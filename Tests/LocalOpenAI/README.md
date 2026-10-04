# Local Images API request probe

This is a disposable Python server for inspecting requests from the mod. It uses only the standard library. It logs request metadata to the console and `logs/requests.jsonl`. It does not call a model or ComfyUI.

Run from the repository root:

```powershell
python -B Tests/LocalOpenAI/mock_server.py
```

In the mod, select **Local (OpenAI compatible)** for the task being checked. Keep the base URL at `http://127.0.0.1:8000/v1`, leave the API key blank, and use distinct model names such as `rim-base`, `rim-expression`, and `rim-regenerate`. Generate on a disposable test save: the probe returns a colored checkerboard that the mod will save as a portrait.

| Response color | Request seen by the probe |
| --- | --- |
| Green | JSON `/v1/images/generations` without images |
| Blue | Multipart `/v1/images/edits` with one or more `image[]` files |
| Purple | Multipart `/v1/images/edits` with a `mask` file |

Each JSON log line records the route, model alias, prompt length and short hash, ordered image sizes and short hashes, mask presence, option names, HTTP status, and elapsed time. It records only whether an Authorization header was present; it does not record the key, prompt text, or image bytes. Logs are ignored by Git. Delete `Tests/LocalOpenAI/logs/` when finished.

To check the probe independently:

```powershell
python -B -m unittest discover -s Tests/LocalOpenAI -p "test_*.py" -v
```

The probe accepts requests up to 64 MiB with `Content-Length` or chunked transfer. It sends a synchronous OpenAI-style `data[0].b64_json` response. It does not evaluate image quality, enforce mask boundaries, or test the actual model bridge. See [LocalSupport.md](../../LocalSupport.md) for the mod-side request contract.
