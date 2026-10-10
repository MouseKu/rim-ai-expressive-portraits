"""The preview keeps only bounded thumbnails and does not save pawn images."""

import base64
import importlib.util
import io
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1] / "rim_ai_portrait"
spec = importlib.util.spec_from_file_location("input_preview", ROOT / "input_preview.py")
input_preview = importlib.util.module_from_spec(spec)
spec.loader.exec_module(input_preview)


def png(size, color):
    output = io.BytesIO()
    Image.new("RGB", size, color).save(output, format="PNG")
    return output.getvalue()


class InputPreviewTests(unittest.TestCase):
    def test_primary_and_references_keep_order_as_small_images(self):
        input_preview.remember("preview-test", [png((1000, 500), "red"), png((800, 800), "blue")])
        previews = input_preview.latest("preview-test")
        self.assertEqual(len(previews), 2)
        with Image.open(io.BytesIO(base64.b64decode(previews[0]))) as image:
            self.assertEqual(image.size, (320, 160))
            self.assertEqual(image.getpixel((0, 0)), (255, 0, 0))
        with Image.open(io.BytesIO(base64.b64decode(previews[1]))) as image:
            self.assertEqual(image.size, (320, 320))
            self.assertEqual(image.getpixel((0, 0)), (0, 0, 255))

    def test_empty_model_has_no_preview(self):
        self.assertEqual(input_preview.latest("not-seen"), [])


if __name__ == "__main__":
    unittest.main()
