"""Small, in-memory previews of the most recent RimWorld image request."""

import base64
import io
from collections import OrderedDict
from threading import Lock

from PIL import Image, ImageOps


MAX_MODELS = 4
MAX_PREVIEW_SIZE = (320, 320)
MAX_REPLAY_BYTES = 64 * 1024 * 1024
_recent = OrderedDict()
_replay = OrderedDict()
_lock = Lock()


def _thumbnail(raw):
    with Image.open(io.BytesIO(raw)) as source:
        image = ImageOps.exif_transpose(source).convert("RGB")
        image.thumbnail(MAX_PREVIEW_SIZE)
        output = io.BytesIO()
        image.save(output, format="PNG")
        return base64.b64encode(output.getvalue()).decode("ascii")


def remember(model, images):
    previews = [_thumbnail(raw) for raw in images]
    with _lock:
        _recent[model] = previews
        _recent.move_to_end(model)
        while len(_recent) > MAX_MODELS:
            _recent.popitem(last=False)


def latest(model):
    with _lock:
        return list(_recent.get(model, []))


def remember_payload(model, prompt, options, images, mask):
    if sum(len(image) for image in images) + len(mask or b"") > MAX_REPLAY_BYTES:
        with _lock:
            _replay.pop(model, None)
        return
    payload = {"prompt": prompt, "options": options, "images": tuple(images), "mask": mask}
    with _lock:
        _replay[model] = payload
        _replay.move_to_end(model)
        while len(_replay) > MAX_MODELS:
            _replay.popitem(last=False)


def latest_payload(model):
    with _lock:
        return _replay.get(model)
