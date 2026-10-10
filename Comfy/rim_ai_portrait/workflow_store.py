"""Validate and store API workflows without changing ComfyUI's saved graphs."""

import json
import os
import tempfile
from pathlib import Path

from .contract import RequestError, model_name


MAX_WORKFLOW_BYTES = 4 * 1024 * 1024


def validate_workflow(workflow):
    if not isinstance(workflow, dict) or not workflow:
        raise RequestError("workflow must be in ComfyUI API format")
    requests = [node for node in workflow.values()
                if isinstance(node, dict) and node.get("class_type") == "RimPortraitRequest"]
    responses = [key for key, node in workflow.items()
                 if isinstance(node, dict) and node.get("class_type") == "RimPortraitResponse"]
    if len(requests) != 1 or len(responses) != 1:
        raise RequestError("workflow needs exactly one Rim Portrait Request and Response node")
    if not all(isinstance(node, dict) and isinstance(node.get("class_type"), str)
               and isinstance(node.get("inputs"), dict) for node in workflow.values()):
        raise RequestError("workflow must be in ComfyUI API format")
    return responses[0]


def save_workflow(directory, model, workflow):
    model_name(model)
    validate_workflow(workflow)
    encoded = json.dumps(workflow, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    if len(encoded) > MAX_WORKFLOW_BYTES:
        raise RequestError("workflow exceeds its size limit")
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    target = directory / (model + ".json")
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="wb", dir=str(directory),
                                         prefix=".rim-workflow-", suffix=".tmp", delete=False) as output:
            temporary = output.name
            output.write(encoded)
        os.replace(temporary, str(target))
    finally:
        if temporary and os.path.exists(temporary):
            os.unlink(temporary)
    return target
