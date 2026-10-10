"""Small checks that run without installing ComfyUI or starting a model."""

import base64
import importlib.util
import io
import sys
import types
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1] / "rim_ai_portrait"
package = types.ModuleType("rim_ai_portrait")
package.__path__ = [str(ROOT)]
sys.modules.setdefault("rim_ai_portrait", package)


def load(name):
    qualified = "rim_ai_portrait." + name
    spec = importlib.util.spec_from_file_location(qualified, ROOT / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    sys.modules[qualified] = module
    spec.loader.exec_module(module)
    return module


contract = load("contract")
input_preview = load("input_preview")
nodes = load("nodes")


def png(color, mode="RGB"):
    buffer = io.BytesIO()
    Image.new(mode, (2, 2), color).save(buffer, format="PNG")
    return base64.b64encode(buffer.getvalue()).decode("ascii")


class BridgeTests(unittest.TestCase):
    def test_contract_preserves_options_and_order(self):
        first, second = b"first", b"second"
        result = contract.parse_fields({"model": "base", "prompt": "portrait",
                                        "output_format": "png", "seed": "42"},
                                       [first, second], None)
        self.assertEqual(result["images"], [first, second])
        self.assertEqual(result["options"], '{"seed": "42"}')

    def test_model_cannot_escape_workflow_folder(self):
        for name in ("../secret", "x/y", "", "a\\b"):
            with self.assertRaises(contract.RequestError):
                contract.model_name(name)

    def test_inpaint_alpha_becomes_white_edit_mask(self):
        source = png((10, 20, 30))
        mask = png((255, 255, 255, 0), "RGBA")
        values = nodes.RimPortraitRequest().unpack("edit", "base", "{}",
                                                  '["' + source + '"]', mask)
        self.assertEqual(tuple(values[2].shape), (1, 2, 2, 3))
        self.assertEqual(float(values[3][0, 0, 0]), 1.0)
        self.assertTrue(values[5])

    def test_response_is_png(self):
        source = nodes.decode_image(png((10, 20, 30)))
        result = nodes.RimPortraitResponse().send(source)
        raw = base64.b64decode(result["ui"]["rim_portrait_png"][0])
        self.assertTrue(raw.startswith(b"\x89PNG\r\n\x1a\n"))

    def test_request_exposes_tag_sections_without_changing_existing_outputs(self):
        values = nodes.RimPortraitRequest().unpack(
            "portrait\n\n[positive_tag]\nblue hair, smiling\n\n[negative_tag]\nhat, blur",
            "rim-base", "{}", "[]", "")
        self.assertEqual(values[0], "portrait")
        self.assertFalse(values[5])
        self.assertEqual(values[6], "blue hair, smiling")
        self.assertEqual(values[7], "hat, blur")
        self.assertEqual(values[8], 1.0)

    def test_request_exposes_local_inpaint_weight(self):
        values = nodes.RimPortraitRequest().unpack(
            "portrait\n\n[positive_tag]\nblue hair\n\n[weight]\n0.7",
            "rim-base", "{}", "[]", "")
        self.assertEqual(values[0], "portrait")
        self.assertEqual(values[6], "blue hair")
        self.assertEqual(values[8], 0.7)
        with self.assertRaisesRegex(ValueError, "weight must be"):
            nodes.RimPortraitRequest().unpack(
                "portrait\n[weight]\ninvalid", "rim-base", "{}", "[]", "")

    def test_manual_queue_replays_last_request(self):
        source = base64.b64decode(png((10, 20, 30)))
        input_preview.remember_payload(
            "replay-test", "portrait\n[positive_tag]\nblue hair\n[negative_tag]\nhat",
            '{"seed": 42}', [source], None)
        values = nodes.RimPortraitRequest().unpack(
            "", "replay-test", "{}", "[]", "", True)
        self.assertEqual(values[0], "portrait")
        self.assertEqual(values[1], '{"seed": 42}')
        self.assertEqual(tuple(values[2].shape), (1, 2, 2, 3))
        self.assertTrue(values[5])
        self.assertEqual(values[6:], ("blue hair", "hat", 1.0))

        live = nodes.RimPortraitRequest().unpack(
            "live", "replay-test", "{}", "[]", "", False)
        self.assertEqual(live[0], "live")
        self.assertFalse(live[5])

    def test_manual_queue_without_saved_request_reports_model(self):
        with self.assertRaisesRegex(ValueError, "no-request-test"):
            nodes.RimPortraitRequest().unpack(
                "", "no-request-test", "{}", "[]", "", True)


if __name__ == "__main__":
    unittest.main()
