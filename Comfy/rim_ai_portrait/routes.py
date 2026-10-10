"""OpenAI Images API routes backed by named ComfyUI API workflows."""

import asyncio
import base64
import json
import os
import time
from pathlib import Path

import aiohttp
from aiohttp import web
from server import PromptServer
from yarl import URL

from .contract import MAX_IMAGE_BYTES, RequestError, parse_fields
from .input_preview import latest, latest_payload, remember, remember_payload
from .workflow_store import MAX_WORKFLOW_BYTES, save_workflow, validate_workflow


WORKFLOWS = Path(__file__).resolve().parent / "workflows"
MAX_WAIT_SECONDS = 3600


def error(status, message):
    return web.json_response({"error": {"message": message}}, status=status)


async def read_part(part, limit):
    chunks = []
    total = 0
    while not part.at_eof():
        chunk = await part.read_chunk()
        total += len(chunk)
        if total > limit:
            raise RequestError("request field exceeds its size limit")
        chunks.append(chunk)
    return b"".join(chunks)


async def read_request(request, edits):
    content_type = request.content_type
    if edits:
        if content_type != "multipart/form-data":
            raise RequestError("edits require multipart/form-data")
        reader = await request.multipart()
        fields, images, mask = {}, [], None
        async for part in reader:
            name = part.name
            if name in ("image[]", "mask"):
                raw = await read_part(part, MAX_IMAGE_BYTES)
                if not raw:
                    raise RequestError("image or mask is empty")
                if name == "mask":
                    if mask is not None:
                        raise RequestError("only one mask is supported")
                    mask = raw
                else:
                    images.append(raw)
                    if len(images) > 16:
                        raise RequestError("at most 16 image[] files are supported")
            elif name:
                raw = await read_part(part, 100000)
                try:
                    fields[name] = raw.decode("utf-8")
                except UnicodeError:
                    raise RequestError("text field is not UTF-8")
        if not images:
            raise RequestError("edits require at least one image[]")
    else:
        if content_type != "application/json":
            raise RequestError("generations require application/json")
        try:
            fields = await request.json()
        except (ValueError, UnicodeError):
            raise RequestError("invalid JSON body")
        if not isinstance(fields, dict):
            raise RequestError("JSON body must be an object")
        images, mask = [], None
    return parse_fields(fields, images, mask)


def workflow_for(payload):
    path = WORKFLOWS / (payload["model"] + ".json")
    if not path.is_file():
        raise RequestError("unknown model/workflow: " + payload["model"])
    with path.open("r", encoding="utf-8") as source:
        workflow = json.load(source)
    if isinstance(workflow, dict) and "prompt" in workflow:
        workflow = workflow["prompt"]
    if not isinstance(workflow, dict):
        raise RequestError("workflow must be exported in ComfyUI API format")
    output_id = validate_workflow(workflow)
    ingress = next(node for node in workflow.values()
                   if node["class_type"] == "RimPortraitRequest")
    inputs = ingress["inputs"]
    inputs.update({
        "prompt": payload["prompt"], "model": payload["model"],
        "options_json": payload["options"],
        "images_json": json.dumps([base64.b64encode(image).decode("ascii")
                                   for image in payload["images"]]),
        "mask_base64": base64.b64encode(payload["mask"]).decode("ascii")
                       if payload["mask"] is not None else "",
        "use_last_request": False,
    })
    return workflow, output_id


def local_url(request):
    address = request.transport.get_extra_info("sockname")
    if not address:
        raise RuntimeError("could not locate the ComfyUI listener")
    host, port = address[:2]
    if host in ("0.0.0.0", "::"):
        host = "127.0.0.1"
    return str(URL.build(scheme="http", host=host, port=port))


async def run_workflow(request, workflow, output_id):
    base = local_url(request)
    timeout = aiohttp.ClientTimeout(total=MAX_WAIT_SECONDS + 30)
    prompt_id = None
    try:
        async with aiohttp.ClientSession(timeout=timeout) as session:
            async with session.post(base + "/prompt", json={"prompt": workflow}) as response:
                result = await response.json(content_type=None)
                if response.status != 200:
                    detail = result.get("error", "workflow validation failed")
                    raise RequestError(str(detail)[:1000])
                prompt_id = result.get("prompt_id")
                if not prompt_id:
                    raise RuntimeError("ComfyUI did not return prompt_id")
            deadline = time.monotonic() + MAX_WAIT_SECONDS
            while time.monotonic() < deadline:
                await asyncio.sleep(0.25)
                history = PromptServer.instance.prompt_queue.get_history(prompt_id=prompt_id)
                item = history.get(prompt_id)
                if not item:
                    continue
                status = item.get("status", {})
                if status.get("status_str") == "error":
                    raise RuntimeError("ComfyUI workflow execution failed; inspect its console")
                values = item.get("outputs", {}).get(output_id, {}).get("rim_portrait_png", [])
                if not values:
                    raise RuntimeError("Rim Portrait Response did not produce a PNG")
                return values[0]
        raise TimeoutError("ComfyUI workflow did not finish within 3600 seconds")
    finally:
        if prompt_id:
            PromptServer.instance.prompt_queue.delete_history_item(prompt_id)


async def handle(request, edits):
    expected_key = os.environ.get("RIM_PORTRAIT_API_KEY", "")
    if expected_key and request.headers.get("Authorization") != "Bearer " + expected_key:
        return error(401, "invalid API key")
    try:
        payload = await read_request(request, edits)
        workflow, output_id = workflow_for(payload)
        remember_payload(payload["model"], payload["prompt"], payload["options"],
                         payload["images"], payload["mask"])
        try:
            remember(payload["model"], payload["images"])
        except (OSError, ValueError):
            pass
        encoded = await run_workflow(request, workflow, output_id)
        return web.json_response({"created": int(time.time()),
                                  "data": [{"b64_json": encoded}]})
    except RequestError as exc:
        return error(400, str(exc))
    except TimeoutError as exc:
        return error(504, str(exc))
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        return error(500, "workflow could not be loaded or executed: " + str(exc)[:500])
    except Exception:
        return error(500, "ComfyUI workflow failed; inspect its console")


def register_routes():
    @PromptServer.instance.routes.get("/rim_portrait/last-request/{model}")
    async def last_request(request):
        try:
            from .contract import model_name
            model = model_name(request.match_info["model"])
            payload = latest_payload(model)
            if payload is None:
                return web.json_response({"received": False})
            return web.json_response({"received": True, "prompt": payload["prompt"],
                                      "options": payload["options"], "images": latest(model),
                                      "has_mask": payload["mask"] is not None})
        except RequestError as exc:
            return error(400, str(exc))

    @PromptServer.instance.routes.get("/rim_portrait/last-images/{model}")
    async def last_images(request):
        try:
            from .contract import model_name
            model = model_name(request.match_info["model"])
            return web.json_response({"images": latest(model)})
        except RequestError as exc:
            return error(400, str(exc))

    @PromptServer.instance.routes.post("/rim_portrait/workflows")
    async def register_workflow(request):
        try:
            if request.content_length is not None and request.content_length > MAX_WORKFLOW_BYTES:
                raise RequestError("workflow exceeds its size limit")
            raw = await request.content.read(MAX_WORKFLOW_BYTES + 1)
            if len(raw) > MAX_WORKFLOW_BYTES:
                raise RequestError("workflow exceeds its size limit")
            data = json.loads(raw)
            if not isinstance(data, dict):
                raise RequestError("workflow registration must be a JSON object")
            path = save_workflow(WORKFLOWS, data.get("model"), data.get("workflow"))
            return web.json_response({"model": path.stem})
        except (RequestError, ValueError, UnicodeError) as exc:
            return error(400, str(exc))

    @PromptServer.instance.routes.post("/v1/images/generations")
    async def generations(request):
        return await handle(request, False)

    @PromptServer.instance.routes.post("/v1/images/edits")
    async def edits(request):
        return await handle(request, True)
