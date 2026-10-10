"""Check the preview route without starting ComfyUI or loading a model."""

import asyncio
import importlib.util
import sys
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


class Routes:
    def __init__(self):
        self.handlers = {}

    def get(self, path):
        return lambda handler: self.handlers.setdefault(path, handler)

    def post(self, path):
        return lambda handler: self.handlers.setdefault(path, handler)


route_table = Routes()
server = types.ModuleType("server")
server.PromptServer = types.SimpleNamespace(instance=types.SimpleNamespace(routes=route_table))
sys.modules["server"] = server
aiohttp = types.ModuleType("aiohttp")
aiohttp.web = types.SimpleNamespace(json_response=lambda data, status=200: (status, data))
sys.modules["aiohttp"] = aiohttp
yarl = types.ModuleType("yarl")
yarl.URL = object
sys.modules["yarl"] = yarl

load("contract")
preview = load("input_preview")
load("workflow_store")
routes = load("routes")
routes.register_routes()


class PreviewRouteTests(unittest.TestCase):
    def test_last_request_returns_prompt_options_and_images(self):
        preview.remember_payload("preview-route-test", "portrait", '{"seed": 1}', [], None)
        preview.remember("preview-route-test", [])
        request = types.SimpleNamespace(match_info={"model": "preview-route-test"})
        handler = route_table.handlers["/rim_portrait/last-request/{model}"]
        status, data = asyncio.run(handler(request))
        self.assertEqual(status, 200)
        self.assertEqual(data, {"received": True, "prompt": "portrait",
                                "options": '{"seed": 1}', "images": [], "has_mask": False})


if __name__ == "__main__":
    unittest.main()
