"""Request validation independent of ComfyUI, for fast contract checks."""

import json
import re


MAX_IMAGES = 16
MAX_IMAGE_BYTES = 32 * 1024 * 1024
MODEL_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$")


class RequestError(ValueError):
    pass


def model_name(value):
    if not isinstance(value, str) or not MODEL_NAME.fullmatch(value):
        raise RequestError("model must be a simple workflow name (letters, digits, _, -, .)")
    if value in (".", ".."):
        raise RequestError("invalid model name")
    return value


def parse_fields(fields, images, mask):
    model = model_name(fields.get("model"))
    prompt = fields.get("prompt")
    if not isinstance(prompt, str) or not prompt.strip():
        raise RequestError("prompt is required")
    if len(prompt) > 100000:
        raise RequestError("prompt is too long")
    if len(images) > MAX_IMAGES:
        raise RequestError("at most 16 image[] files are supported")
    if mask is not None and not images:
        raise RequestError("mask requires image[]")
    if fields.get("output_format", "png") != "png":
        raise RequestError("only PNG output is supported")
    options = {key: value for key, value in fields.items()
               if key not in ("model", "prompt", "output_format")}
    for key, value in options.items():
        if len(key) > 100 or len(str(value)) > 10000:
            raise RequestError("option is too large")
    return {"model": model, "prompt": prompt, "options": json.dumps(options),
            "images": images, "mask": mask}
