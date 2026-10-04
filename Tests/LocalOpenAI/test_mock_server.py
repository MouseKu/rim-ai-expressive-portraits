"""Contract checks for the standalone local Images API probe."""

import base64
import http.client
import json
import tempfile
import threading
import unittest
from pathlib import Path

from mock_server import ProbeServer


class ProbeContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory()
        cls.log_file = Path(cls.temporary.name) / "requests.jsonl"
        cls.server = ProbeServer(("127.0.0.1", 0), cls.log_file, 256)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join(timeout=5)
        cls.temporary.cleanup()

    def request(self, path, content_type, body, authorization=None, chunked=False):
        connection = http.client.HTTPConnection("127.0.0.1", self.server.server_port, timeout=5)
        headers = {"Content-Type": content_type}
        if authorization:
            headers["Authorization"] = authorization
        if chunked:
            headers["Transfer-Encoding"] = "chunked"
        connection.request("POST", path, body, headers, encode_chunked=chunked)
        response = connection.getresponse()
        result = response.status, json.loads(response.read().decode("utf-8"))
        connection.close()
        return result

    def latest_log(self):
        return json.loads(self.log_file.read_text(encoding="utf-8").splitlines()[-1])

    def assert_png_response(self, response):
        image = base64.b64decode(response["data"][0]["b64_json"], validate=True)
        self.assertTrue(image.startswith(b"\x89PNG\r\n\x1a\n"))

    def multipart(self, fields, images, mask=None):
        boundary = "rim-local-probe-boundary"
        chunks = []
        for name, value in fields.items():
            chunks.append(("--{}\r\nContent-Disposition: form-data; name=\"{}\"\r\n\r\n{}\r\n"
                           .format(boundary, name, value)).encode("utf-8"))
        for image in images:
            chunks.append(("--{}\r\nContent-Disposition: form-data; name=\"image[]\"; filename=\"input.png\"\r\n"
                           "Content-Type: image/png\r\n\r\n".format(boundary)).encode("ascii"))
            chunks.extend((image, b"\r\n"))
        if mask is not None:
            chunks.append(("--{}\r\nContent-Disposition: form-data; name=\"mask\"; filename=\"mask.png\"\r\n"
                           "Content-Type: image/png\r\n\r\n".format(boundary)).encode("ascii"))
            chunks.extend((mask, b"\r\n"))
        chunks.append(("--{}--\r\n".format(boundary)).encode("ascii"))
        return "multipart/form-data; boundary=" + boundary, b"".join(chunks)

    def test_text_only_generation_logs_route_without_key(self):
        body = json.dumps({"model": "rim-base", "prompt": "Test base portrait",
                           "output_format": "png", "size": "512x512"}).encode("utf-8")
        status, response = self.request("/v1/images/generations", "application/json", body,
                                        "Bearer secret-test-token")
        self.assertEqual(status, 200)
        self.assert_png_response(response)
        entry = self.latest_log()
        self.assertEqual((entry["model"], entry["result"], entry["image_count"]),
                         ("rim-base", "generation", 0))
        self.assertTrue(entry["authorization_present"])
        self.assertNotIn("secret-test-token", self.log_file.read_text(encoding="utf-8"))
        self.assertNotIn("Test base portrait", self.log_file.read_text(encoding="utf-8"))

    def test_edit_preserves_input_order_in_log(self):
        content_type, body = self.multipart(
            {"model": "rim-expression", "prompt": "Edit expression", "output_format": "png"},
            [b"first image", b"second image"])
        status, response = self.request("/v1/images/edits", content_type, body)
        self.assertEqual(status, 200)
        self.assert_png_response(response)
        entry = self.latest_log()
        self.assertEqual(entry["result"], "edit")
        self.assertEqual([image["bytes"] for image in entry["images"]], [11, 12])
        self.assertEqual([image["index"] for image in entry["images"]], [1, 2])
        self.assertFalse(entry["mask_present"])

    def test_masked_edit_logs_mask_and_returns_image(self):
        content_type, body = self.multipart(
            {"model": "rim-regenerate", "prompt": "Close eyes"}, [b"source"], b"mask")
        status, response = self.request("/v1/images/edits", content_type, body)
        self.assertEqual(status, 200)
        self.assert_png_response(response)
        entry = self.latest_log()
        self.assertEqual(entry["result"], "inpaint")
        self.assertEqual(entry["model"], "rim-regenerate")
        self.assertTrue(entry["mask_present"])
        self.assertEqual(entry["mask"]["bytes"], 4)

    def test_missing_image_returns_error(self):
        content_type, body = self.multipart({"model": "rim-base", "prompt": "Try edit"}, [])
        status, response = self.request("/v1/images/edits", content_type, body)
        self.assertEqual(status, 400)
        self.assertIn("image[]", response["error"]["message"])

    def test_chunked_generation(self):
        body = json.dumps({"model": "rim-base", "prompt": "Chunked test"}).encode("utf-8")
        status, response = self.request("/v1/images/generations", "application/json", body, chunked=True)
        self.assertEqual(status, 200)
        self.assert_png_response(response)
        self.assertEqual(self.latest_log()["result"], "generation")


if __name__ == "__main__":
    unittest.main()
