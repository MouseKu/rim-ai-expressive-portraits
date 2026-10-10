"""Checks the install-relative workflow cache without starting ComfyUI."""

import importlib.util
import json
import sys
import tempfile
import types
import unittest
from pathlib import Path


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
workflow_store = load("workflow_store")


def workflow():
    return {
        "1": {"class_type": "RimPortraitRequest", "inputs": {"model": "rim-base"}},
        "2": {"class_type": "RimPortraitResponse", "inputs": {"image": ["1", 2]}},
    }


class WorkflowStoreTests(unittest.TestCase):
    def test_saves_api_workflow_under_install_relative_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            target = workflow_store.save_workflow(Path(directory) / "workflows", "rim-base", workflow())
            self.assertEqual(target.name, "rim-base.json")
            self.assertEqual(json.loads(target.read_text(encoding="utf-8")), workflow())

    def test_invalid_graph_does_not_replace_existing_workflow(self):
        with tempfile.TemporaryDirectory() as directory:
            target = workflow_store.save_workflow(directory, "rim-base", workflow())
            original = target.read_bytes()
            with self.assertRaises(contract.RequestError):
                workflow_store.save_workflow(directory, "rim-base", {"nodes": []})
            self.assertEqual(target.read_bytes(), original)

    def test_model_cannot_escape_workflow_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(contract.RequestError):
                workflow_store.save_workflow(directory, "../other", workflow())


if __name__ == "__main__":
    unittest.main()
