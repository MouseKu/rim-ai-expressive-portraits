"""Workflow input/output nodes. Images use ComfyUI's BHWC float tensors."""

import base64
import io
import json
import math
import re

import numpy as np
import torch
from PIL import Image, ImageOps

from .input_preview import latest_payload


def decode_image(encoded):
    data = base64.b64decode(encoded, validate=True)
    with Image.open(io.BytesIO(data)) as opened:
        image = ImageOps.exif_transpose(opened).convert("RGB")
        return torch.from_numpy(np.array(image).astype(np.float32) / 255.0)[None,]


class RimPortraitRequest:
    @classmethod
    def INPUT_TYPES(cls):
        return {"required": {
            "prompt": ("STRING", {"default": "", "multiline": True}),
            "model": ("STRING", {"default": "rim-base"}),
            "options_json": ("STRING", {"default": "{}", "multiline": True}),
            "images_json": ("STRING", {"default": "[]", "multiline": True}),
            "mask_base64": ("STRING", {"default": "", "multiline": True}),
            "use_last_request": ("BOOLEAN", {"default": True}),
        }}

    RETURN_TYPES = ("STRING", "STRING", "IMAGE", "MASK", "RIM_PORTRAIT_IMAGES", "BOOLEAN",
                    "STRING", "STRING", "FLOAT")
    RETURN_NAMES = ("prompt", "options_json", "primary_image", "inpaint_mask",
                    "reference_images", "has_primary_image", "positive_tag", "negative_tag", "weight")
    FUNCTION = "unpack"
    CATEGORY = "Rim AI Portrait"

    def unpack(self, prompt, model, options_json, images_json, mask_base64, use_last_request=True):
        if use_last_request:
            saved = latest_payload(model)
            if saved is not None:
                if not prompt.strip():
                    prompt = saved["prompt"]
                if options_json == "{}":
                    options_json = saved["options"]
                if images_json == "[]":
                    images_json = json.dumps([base64.b64encode(image).decode("ascii")
                                              for image in saved["images"]])
                if not mask_base64 and saved["mask"] is not None:
                    mask_base64 = base64.b64encode(saved["mask"]).decode("ascii")
            elif not prompt.strip() and images_json == "[]":
                raise ValueError("No RimWorld request has been received for model " + model)
        encoded_images = json.loads(images_json)
        images = [decode_image(value) for value in encoded_images]
        primary = images[0] if images else torch.zeros((1, 512, 512, 3), dtype=torch.float32)
        if mask_base64:
            raw = base64.b64decode(mask_base64, validate=True)
            with Image.open(io.BytesIO(raw)) as opened:
                rgba = opened.convert("RGBA")
                if rgba.size != (primary.shape[2], primary.shape[1]):
                    raise ValueError("inpaint mask and primary image dimensions differ")
                # OpenAI: transparent pixels are edited. ComfyUI: white mask is edited.
                alpha = np.array(rgba.getchannel("A")).astype(np.float32) / 255.0
                mask = torch.from_numpy(1.0 - alpha)[None,]
        else:
            mask = torch.zeros((1, primary.shape[1], primary.shape[2]), dtype=torch.float32)
        positive = re.search(r"(?:^|\n)\[positive_tag\][ \t]*\r?\n([^\r\n]*)", prompt)
        negative = re.search(r"(?:^|\n)\[negative_tag\][ \t]*\r?\n([^\r\n]*)", prompt)
        weight_section = re.search(r"(?:^|\n)\[weight\][ \t]*\r?\n([^\r\n]*)", prompt)
        weight = 1.0
        if weight_section:
            try:
                weight = float(weight_section.group(1).strip())
            except ValueError:
                raise ValueError("Rim Portrait Request weight must be a number from 0.1 to 1.0")
            if not math.isfinite(weight) or not 0.1 <= weight <= 1.0:
                raise ValueError("Rim Portrait Request weight must be a number from 0.1 to 1.0")
        clean_prompt = re.sub(r"(?:\r?\n)*\[(?:positive_tag|negative_tag|weight)\][ \t]*\r?\n[^\r\n]*",
                              "", prompt).strip()
        return (clean_prompt, options_json, primary, mask, images[1:], bool(images),
                positive.group(1).strip() if positive else "",
                negative.group(1).strip() if negative else "", weight)

    @classmethod
    def IS_CHANGED(cls, **kwargs):
        # Repeated identical prompts should still render a fresh portrait.
        return float("nan")


class RimPortraitReference:
    @classmethod
    def INPUT_TYPES(cls):
        return {"required": {"images": ("RIM_PORTRAIT_IMAGES",),
                             "index": ("INT", {"default": 1, "min": 1, "max": 15})}}

    RETURN_TYPES = ("IMAGE", "BOOLEAN")
    RETURN_NAMES = ("image", "available")
    FUNCTION = "select"
    CATEGORY = "Rim AI Portrait"

    def select(self, images, index):
        available = index <= len(images)
        return (images[index - 1] if available else torch.zeros((1, 512, 512, 3)), available)


class RimPortraitResponse:
    @classmethod
    def INPUT_TYPES(cls):
        return {"required": {"image": ("IMAGE",)}}

    RETURN_TYPES = ()
    FUNCTION = "send"
    OUTPUT_NODE = True
    CATEGORY = "Rim AI Portrait"

    def send(self, image):
        if image.shape[0] < 1:
            raise ValueError("response image batch is empty")
        pixels = (image[0].cpu().numpy().clip(0, 1) * 255).astype(np.uint8)
        buffer = io.BytesIO()
        Image.fromarray(pixels).save(buffer, format="PNG")
        return {"ui": {"rim_portrait_png": [base64.b64encode(buffer.getvalue()).decode("ascii")]}}


NODE_CLASS_MAPPINGS = {
    "RimPortraitRequest": RimPortraitRequest,
    "RimPortraitReference": RimPortraitReference,
    "RimPortraitResponse": RimPortraitResponse,
}
NODE_DISPLAY_NAME_MAPPINGS = {
    "RimPortraitRequest": "Rim Portrait Request",
    "RimPortraitReference": "Rim Portrait Reference",
    "RimPortraitResponse": "Rim Portrait Response",
}
