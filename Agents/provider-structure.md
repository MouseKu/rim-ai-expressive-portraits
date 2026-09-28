# Image provider structure

This guide explains where image providers fit in RIM AI Expressive Portraits and what to change when adding one. It is for users who want to understand provider behavior and agents implementing provider support. The code targets RimWorld 1.5/1.6, C# 7.3, and .NET Framework 4.7.2.

## Current providers

| Settings choice (`AiProvider`) | Implementation | Main configuration |
| --- | --- | --- |
| OpenAI (`OpenAI`) | `Source/RimAIPortrait/Providers/OpenAI/OpenAiPortraitProvider.cs` | API key, model, options JSON |
| Google (`Google`) | `Source/RimAIPortrait/Providers/Google/GooglePortraitProvider.cs` | API key, model, options JSON |
| Local ComfyUI (`LocalComfyUI`) | `Source/RimAIPortrait/Providers/Comfy/ComfyUiPortraitProvider.cs` | Server URL, workflow, negative prompt, timeout |
| NovelAI (`NovelAI`) | `Source/RimAIPortrait/Providers/NAI/NovelAiPortraitProvider.cs` | API key, model, options JSON; inpaint strength for expressions |

Users choose Base and Expression providers independently in settings. Each choice has its own model, credentials or connection details, options, and style folder where applicable. The same provider may be selected for both, but the two configurations are read separately. `Source/RimAIPortrait/GUI/ProviderSettingsUi.cs` renders these controls; `Source/RimAIPortrait/Settings/Settings.cs` persists them.

## Request flow and ownership

1. `Source/RimAIPortrait/Portraits/PortraitRuntime.cs` chooses `settings.Provider` for `Normal` and `settings.ExpressionProvider` for other states. It builds the prompt with `PortraitPrompt`, obtains the primary image and selected references, and calls the selected provider's `CreateConfiguration`.
2. The runtime creates a `PortraitGenerationRequest`, calls `StyleReferences(request)`, then limits those paths with `StyleReferenceCapacity(request)`. A provider decides which style files are eligible and how many fit its input limit.
3. If enabled and supported, tag extraction runs before generation. The runtime puts the result in `request.ExtractedTags`. The current integration supports NovelAI and ComfyUI; ComfyUI uses it only when the workflow contains `__RIM_EXTRACTED_TAG__`.
4. The runtime starts the provider's `IEnumerator Generate(request, done)` coroutine. NovelAI Expression uses its additional `GenerateInpaint(...)` path with the selected mask and strength. The provider returns image bytes or an error through `PortraitGenerationResult`.
5. The runtime saves successful images through `PortraitData`. Provider code should not own portrait storage, gallery behavior, or inspect-pane drawing.

`Source/RimAIPortrait/Providers/PortraitGenerationProvider.cs` defines the request, result, typed configurations, `IPortraitGenerationProvider`, and registry. The request includes `State`, `Prompt`, `PrimaryImage`, `ImageUse`, `ReferencePaths`, `StyleFolder`, `StylePaths`, and `Configuration`. It can also carry `InpaintMask`, `InpaintRequestMask`, `InpaintTag`, and `ExtractedTags`. `ImageUse` distinguishes a character reference from an image being edited. Expression requests use the saved Base portrait as their primary image and use `Edit`.

Shared helpers live in `Providers/PortraitProviderUtilities.cs` (HTTP failure checks, short response text, simple options, MIME types, and style files). `PortraitImageCaptions.cs` loads the packaged OpenAI and Google image captions. `PortraitInpaintMask.cs` converts the dialog's mask to an alpha mask for ComfyUI. Provider-specific payloads and response parsing stay in each provider folder. NovelAI's reference-image and composite helpers stay under `Providers/NAI/`.

## Before adding a provider

Establish its official API endpoint and authentication, supported models and options, image input and output formats, input count and size limits, timeout behavior, and whether Base and Expression need different settings. Decide how it handles a pawn sprite, an existing Base portrait, selected references, style images, and expression editing. Verify uncertain API details against official documentation; do not guess request schemas or model names.

Read these files first, plus the existing provider closest to the new API:

- `Source/RimAIPortrait/Providers/PortraitGenerationProvider.cs`
- `Source/RimAIPortrait/Portraits/PortraitRuntime.cs` (`Request`, `CanGenerate`, and `LogDebugRequest`)
- `Source/RimAIPortrait/Settings/Settings.cs`
- `Source/RimAIPortrait/GUI/ProviderSettingsUi.cs`
- `Source/RimAIPortrait/Providers/PortraitProviderUtilities.cs`
- `Resources/Json/default-values.json` and any relevant provider options or caption JSON
- Both `Resources/Languages/English/Keyed/RimAIPortrait.xml` and `Resources/Languages/Korean/Keyed/RimAIPortrait.xml`

Open the prompt loader, inpaint dialog, or tag extractor code when the new provider actually needs those features.

## Add-provider checklist

### 1. Identifier, defaults, and persistence

- Append a value to `AiProvider` in `Settings.cs`; existing enum values are persisted, so do not reorder them.
- Add separate Base and Expression fields for behavior that users can configure independently. Register each field in `ExposeData()` with a stable camelCase Scribe key.
- Add scalar defaults to `Resources/Json/default-values.json`, loaded by `SettingsDefaults`. If the provider uses options JSON, add packaged Base and Expression files under `Resources/Json/` and load them through `ProviderOptionDefaults`. Update `Scripts/build.ps1` when packaging needs another resource.
- Add migration only when an existing saved value must be transformed. Do not rename saved keys without backward-compatible loading.
- If the provider needs its own Base prompt, update `RimAIPortraitMod.PromptDefaults.cs`, `PortraitPrompt.BaseInstructions()`, settings/UI, and the packaged prompt file. Keep the Expression prompt behavior in mind.

### 2. Contract and implementation

- Add a `PortraitProviderConfiguration` subtype in `PortraitGenerationProvider.cs`, then implement `IPortraitGenerationProvider` in `Providers/<Name>/<Name>PortraitProvider.cs`. Register it in `PortraitProviderRegistry`.
- `Id` returns the enum value. `CreateConfiguration(settings, expression)` selects the correct saved fields. `Describe` must omit secrets. `Validate(configuration, kind)` returns a localized error for missing required settings, or `null` when valid; the runtime calls it before generation.
- `StyleReferences(request)` returns eligible style paths, including a packaged fallback only if intended. `StyleReferenceCapacity(request)` returns the number the runtime may use after accounting for other images and provider limits. `Generate` must also enforce its own request limits.
- `Generate(request, done)` uses `IEnumerator` and `UnityWebRequest`, sets explicit timeouts, and calls `done` once on every terminal path. Return image bytes on success or a localized failure on error. Include HTTP status and a shortened response body where useful, without exposing keys, authorization headers, full payloads, or base64 images.
- Respect `ImageUse`, the primary image, selected references, and the supplied `StylePaths`. Support masks or extracted tags only when the API and user flow call for them. Keep provider-specific conversions inside the provider folder or an appropriate shared helper.
- Prefer `byte[]` for image data. Destroy any temporary `Texture2D` instances. Use shared utilities for simple parsing and file handling; use typed DTOs or a provider-specific parser for complex JSON.

### 3. Settings and localization

- Add the new choice to the hard-coded provider cycle, `Label()`, and provider-specific controls in `ProviderSettingsUi.cs`. Bind every control to the correct Base or Expression field, and keep Base Prompt controls in the Base section.
- Use `Localization.T(...)` for user-facing labels, help, status, and errors. Add matching keys in both English and Korean keyed XML files. Reuse shared error keys where they fit.
- If the provider adds an expression selection or inpaint workflow, update the runtime and dialog deliberately. The current selection workflow is specific to NovelAI and ComfyUI; adding a registry entry alone does not enable it.

## Verification

- Confirm the enum, settings defaults, Scribe keys, configuration, registry, selection UI, and both language files agree.
- Confirm Base and Expression can select the provider independently and settings survive a game restart.
- Check requests with the minimum required input and with primary, selected, and style images up to the provider's limit. Check invalid configuration, HTTP failure, and a successful response without an image.
- If applicable, check expression editing, masks, extracted tags, and fallback style behavior. Inspect `Player.log` for exceptions and leaked secrets. Do not make paid API calls just to validate unrelated work.
- Compile Release after any code change:

```powershell
.\Scripts\build.ps1 `
  -Configuration Release `
  -RimWorldDir "C:\path\to\RimWorld" `
  -HarmonyDll "C:\path\to\0Harmony.dll"
```

When handing off an implementation, report the files changed, provider behavior and compatibility decisions, Release build result, and which API or in-game checks were actually run.
