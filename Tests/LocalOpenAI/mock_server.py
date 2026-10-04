"""Local OpenAI Images API probe. No dependencies outside the Python standard library."""

import argparse
import base64
import hashlib
import json
import struct
import threading
import time
import zlib
from datetime import datetime, timezone
from email.parser import BytesParser
from email.policy import default
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


MAX_BODY_BYTES = 64 * 1024 * 1024
COLORS = {
    "generation": ((48, 160, 92, 255), (190, 240, 190, 255)),
    "edit": ((50, 104, 208, 255), (188, 211, 255, 255)),
    "inpaint": ((174, 68, 179, 255), (244, 190, 240, 255)),
}


class RequestError(Exception):
    pass


def png_chunk(name, data):
    return struct.pack(">I", len(data)) + name + data + struct.pack(">I", zlib.crc32(name + data) & 0xFFFFFFFF)


def probe_png(kind, size):
    first, second = COLORS[kind]
    rows = bytearray()
    for y in range(size):
        rows.append(0)
        for x in range(size):
            rows.extend(first if (x // 64 + y // 64) % 2 == 0 else second)
    return (b"\x89PNG\r\n\x1a\n"
            + png_chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + png_chunk(b"IDAT", zlib.compress(bytes(rows), 6))
            + png_chunk(b"IEND", b""))


def summarize_image(data, index, content_type):
    return {
        "index": index,
        "content_type": content_type,
        "bytes": len(data),
        "sha256_12": hashlib.sha256(data).hexdigest()[:12],
    }


class ProbeServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, address, log_file, output_size=512):
        super().__init__(address, ProbeHandler)
        self.log_file = Path(log_file)
        self.log_file.parent.mkdir(parents=True, exist_ok=True)
        self.log_lock = threading.Lock()
        self.responses = {kind: probe_png(kind, output_size) for kind in COLORS}

    def record(self, details):
        details["time_utc"] = datetime.now(timezone.utc).isoformat(timespec="seconds")
        line = json.dumps(details, ensure_ascii=False, sort_keys=True)
        with self.log_lock:
            with self.log_file.open("a", encoding="utf-8") as output:
                output.write(line + "\n")
            print(line, flush=True)


class ProbeHandler(BaseHTTPRequestHandler):
    server_version = "RimLocalProbe/1.0"

    def log_message(self, format_string, *args):
        # The structured JSON line written by record() is the request log.
        return

    def do_GET(self):
        if self.path == "/health":
            self.send_json(200, {"status": "ok"})
        else:
            self.send_json(404, {"error": {"message": "Unknown route"}})

    def do_POST(self):
        started = time.monotonic()
        record = {
            "method": "POST",
            "path": self.path,
            "authorization_present": bool(self.headers.get("Authorization")),
        }
        try:
            body = self.read_body()
            if self.path == "/v1/images/generations":
                kind, details = self.parse_generation(body)
            elif self.path == "/v1/images/edits":
                kind, details = self.parse_edit(body)
            else:
                raise RequestError("Unknown route; use /v1/images/generations or /v1/images/edits")
            record.update(details)
            record["result"] = kind
            record["status"] = 200
            response = {"created": int(time.time()), "data": [
                {"b64_json": base64.b64encode(self.server.responses[kind]).decode("ascii")}]}
        except RequestError as error:
            record["status"] = 400
            record["error"] = str(error)
            response = {"error": {"message": str(error)}}
        except Exception as error:
            record["status"] = 500
            record["error"] = type(error).__name__
            response = {"error": {"message": "Probe server error"}}
        finally:
            record["elapsed_ms"] = round((time.monotonic() - started) * 1000)
            self.server.record(record)
        self.send_json(record["status"], response)

    def read_body(self):
        if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
            body = bytearray()
            while True:
                line = self.rfile.readline(128)
                try:
                    length = int(line.split(b";", 1)[0].strip(), 16)
                except ValueError:
                    raise RequestError("Invalid chunk length")
                if length < 0 or len(body) + length > MAX_BODY_BYTES:
                    raise RequestError("Request body exceeds the 64 MiB probe limit")
                if length == 0:
                    while self.rfile.readline(8192) not in (b"\r\n", b"\n", b""):
                        pass
                    return bytes(body)
                chunk = self.rfile.read(length)
                if len(chunk) != length or self.rfile.read(2) != b"\r\n":
                    raise RequestError("Incomplete chunked request body")
                body.extend(chunk)
        raw_length = self.headers.get("Content-Length")
        if raw_length is None:
            raise RequestError("Content-Length or chunked transfer is required")
        try:
            length = int(raw_length)
        except ValueError:
            raise RequestError("Invalid Content-Length")
        if length < 0 or length > MAX_BODY_BYTES:
            raise RequestError("Request body exceeds the 64 MiB probe limit")
        body = self.rfile.read(length)
        if len(body) != length:
            raise RequestError("Incomplete request body")
        return body

    def parse_generation(self, body):
        if self.headers.get_content_type() != "application/json":
            raise RequestError("Generation requires application/json")
        try:
            payload = json.loads(body.decode("utf-8"))
        except (UnicodeError, ValueError):
            raise RequestError("Invalid JSON body")
        if not isinstance(payload, dict):
            raise RequestError("Generation body must be an object")
        model = payload.get("model")
        prompt = payload.get("prompt")
        if not isinstance(model, str) or not model.strip():
            raise RequestError("Missing model")
        if not isinstance(prompt, str) or not prompt.strip():
            raise RequestError("Missing prompt")
        return "generation", {
            "model": model,
            "prompt_chars": len(prompt),
            "prompt_sha256_12": hashlib.sha256(prompt.encode("utf-8")).hexdigest()[:12],
            "image_count": 0,
            "mask_present": False,
            "output_format": payload.get("output_format"),
            "option_names": sorted(set(payload) - {"model", "prompt", "output_format"}),
        }

    def parse_edit(self, body):
        if self.headers.get_content_type() != "multipart/form-data":
            raise RequestError("Edit requires multipart/form-data")
        envelope = ("MIME-Version: 1.0\r\nContent-Type: "
                    + self.headers.get("Content-Type", "") + "\r\n\r\n").encode("ascii")
        try:
            message = BytesParser(policy=default).parsebytes(envelope + body)
        except (UnicodeError, ValueError):
            raise RequestError("Invalid multipart body")
        if not message.is_multipart():
            raise RequestError("Multipart boundary is missing")
        fields = {}
        images = []
        mask = None
        for part in message.iter_parts():
            name = part.get_param("name", header="content-disposition")
            data = part.get_payload(decode=True) or b""
            if name == "image[]":
                images.append(summarize_image(data, len(images) + 1, part.get_content_type()))
            elif name == "mask":
                if mask is not None:
                    raise RequestError("Only one mask is supported")
                mask = summarize_image(data, 1, part.get_content_type())
            elif name:
                try:
                    fields[name] = data.decode("utf-8")
                except UnicodeError:
                    raise RequestError("Text field is not UTF-8")
        if not fields.get("model", "").strip():
            raise RequestError("Missing model")
        if not fields.get("prompt", "").strip():
            raise RequestError("Missing prompt")
        if not images:
            raise RequestError("Edit requires at least one image[]")
        if mask is not None and mask["content_type"] != "image/png":
            raise RequestError("Mask must be image/png")
        prompt = fields["prompt"]
        return ("inpaint" if mask else "edit"), {
            "model": fields["model"],
            "prompt_chars": len(prompt),
            "prompt_sha256_12": hashlib.sha256(prompt.encode("utf-8")).hexdigest()[:12],
            "image_count": len(images),
            "images": images,
            "mask_present": mask is not None,
            "mask": mask,
            "output_format": fields.get("output_format"),
            "option_names": sorted(set(fields) - {"model", "prompt", "output_format"}),
        }

    def send_json(self, status, value):
        body = json.dumps(value, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass


def main():
    parser = argparse.ArgumentParser(description="Inspect Rim AI Portrait's local Images API requests")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8000)
    parser.add_argument("--size", type=int, default=512, choices=(256, 512, 1024))
    parser.add_argument("--log-file", type=Path,
                        default=Path(__file__).parent / "logs" / "requests.jsonl")
    args = parser.parse_args()
    server = ProbeServer((args.host, args.port), args.log_file, args.size)
    print("Probe ready at http://{}:{}/v1; logging to {}".format(
        args.host, server.server_port, args.log_file), flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("Stopping probe", flush=True)
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
