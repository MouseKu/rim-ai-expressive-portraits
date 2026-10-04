# Local OpenAI-Compatible Image Provider

## Scope

`Local (OpenAI compatible)` shares the OpenAI image request implementation and sends Images API requests to a server configured by the user. A Python bridge is a separate program and is not included in this repository. The existing OpenAI, Google, NovelAI, and direct ComfyUI providers remain available. Local tag extractor settings are independent of portrait generation settings.

`AiProvider.LocalOpenAI` was appended to the existing enum, preserving the numeric values of older provider selections. Base, expression, and regeneration each have their own server URL, optional API key, model name, and options JSON; they share a request timeout. The default URL is `http://127.0.0.1:8000/v1`, and the default timeout is 900 seconds. The default model aliases are `rim-base`, `rim-expression`, and `rim-regenerate`, respectively. A bridge can map those aliases to actual models or ComfyUI workflows, including distinct workflows that use the same model.

The local provider shares request code with OpenAI, but has separate packaged defaults: prompts in `Resources/Prompts/Local/`, image captions in `Resources/Json/local-ai-img-caption.json`, and base and expression options in `Resources/Json/local-base-options.json` and `Resources/Json/local-expression-options.json`. Edited local prompts and options are saved separately from OpenAI settings and are included in current prompt archives.

## HTTP contract

The configured URL is a base URL ending in `/v1`. It must be an absolute HTTP or HTTPS URL without user information, a query, or a fragment. If the API key is empty, no `Authorization` header is sent; otherwise the request uses `Bearer <key>`. The key is not placed in the request body or diagnostic logs. The user can configure a LAN server.

| Input | Request |
| --- | --- |
| One or more input images | `POST {baseUrl}/images/edits` with `multipart/form-data` |
| No input images | `POST {baseUrl}/images/generations` with `application/json` |

An edit request sends `model`, `prompt`, `output_format=png`, additional fields from the options JSON, and `image[]` files. Image order is the primary input, up to eight user-selected references, then up to eight images from the configured style folder, with a limit of 16 images in total. The primary input is the pawn sprite for base generation and an existing portrait for expression generation or regeneration. The prompt includes numbered image captions. Options JSON cannot override `model`, `prompt`, `output_format`, or `mask`. OpenAI-specific size normalization is not applied to local requests.

An expression or regeneration request with a selected mask also sends a standard `mask` PNG file. The white selection in the UI becomes transparent alpha in that file. If no mask is selected, the `mask` field is omitted. The local path sends the mask to the server but does not automatically composite the returned image onto the original. A bridge that must preserve pixels outside the mask has to produce that result itself. NovelAI's separate mask and composite experiment rules do not apply here.

A generation request without images sends JSON containing `model`, `prompt`, `output_format=png`, and additional options. Numbers and `true` or `false` are emitted as JSON primitive values; other option values are strings. The generation route sends no image, mask, or streaming field. Its default options are initially the same as OpenAI's image options, but local changes are stored separately.

The expected response is a non-streaming OpenAI Images response with PNG base64 in `data[0].b64_json`. The shared parser also accepts `data[0].url` and top-level `b64_json`, `result`, or `url` compatibility forms. HTTP failures display the status and a shortened response. Requests use Unity coroutines and `UnityWebRequest`; the configured local timeout is clamped to 60–3600 seconds. There is no job ID or polling protocol.

The mod sends no additional task-type field or dedicated header. A bridge can use the `model` alias to distinguish base, expression, and regeneration, and the presence of `mask` to distinguish masked edits. If two tasks use the same alias and otherwise identical requests, the bridge cannot distinguish them. There are no dedicated Images API fields for ComfyUI-specific values such as `inpaint_strength` or a negative prompt. When inpaint tags are available, the local provider appends positive and negative tag sections to `prompt`. Other bridge-specific behavior needs an explicit contract through options JSON, the prompt, or bridge configuration.

## Settings and archive compatibility

- Local URLs, keys, models, and options are stored in `PortraitSettings` Scribe fields. Existing OpenAI keys and models are not copied automatically.
- Current prompt archive exports use version 8 and include local model aliases, options, image captions, and prompts. Server URLs and API keys are excluded.
- Versions 3–7 remain importable. Version 5 contains local aliases and options, while local prompts and image captions were added in version 6. Importing versions 3 or 4 leaves the current local aliases and options in place.
- Direct ComfyUI settings and workflows are separate. Moving them to a Python bridge and migrating their settings would require separate work.
- Server connectivity and image quality require an in-game check against a running server. Compilation cannot verify the HTTP exchange or mask quality.

## Minimum Python bridge requirements

1. Accept JSON requests at `POST /v1/images/generations` and multipart requests at `POST /v1/images/edits`.
2. Route the `model` aliases to models or workflows, preserve `image[]` order, and interpret an optional `mask`.
3. Return HTTP 200 with `{ "data": [{ "b64_json": "..." }] }` when complete, or an appropriate HTTP error on failure.
4. Respond within the configured timeout. A queued job or polling protocol would require an extension to the mod's contract.

## Inspecting requests with the mock server

`Tests/LocalOpenAI/mock_server.py` is a validation server that needs no third-party Python packages. It returns different colored PNGs for generation, edits, and masked edits instead of calling a model. It writes request summaries to the console and to the Git-ignored `Tests/LocalOpenAI/logs/requests.jsonl`. The log includes the model alias, route, image count, order, sizes and short hashes, mask presence, option names, and status. It does not record API key values, prompt text, or image bytes. See the [mock server README](../Tests/LocalOpenAI/README.md) for instructions. Use a disposable test save because the mock response replaces the portrait in game.

## References

- [OpenAI Images edit API](https://developers.openai.com/api/reference/resources/images/methods/edit)
- [OpenAI Images generation API](https://developers.openai.com/api/reference/resources/images/methods/generate)
