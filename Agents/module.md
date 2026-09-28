# Source module map

This document maps the current `Source/RimAIPortrait/` tree to its responsibilities. Start here when deciding where a change belongs. For the steps to add a provider, see [provider-structure.md](provider-structure.md).

The mod targets RimWorld 1.5/1.6, .NET Framework 4.7.2, and C# 7.3. All source files use the `RimAIPortrait` namespace even though they are grouped into folders.

## Modules at a glance

| Location | Owns | Main files |
| --- | --- | --- |
| Root | Mod startup, shared localization, project configuration | `RimAIPortraitMod.cs`, `Localization.cs`, `RimAIPortrait.csproj` |
| `GUI/` | Settings, dialogs, inspect-pane drawing, pawn command patches | `RimAIPortraitMod.SettingsUi.cs`, `ProviderSettingsUi.cs`, `PromptSettingsUi.cs`, `Dialog_*.cs`, `InspectPanePatch.cs`, `PortraitGizmoPatch.cs` |
| `Portraits/` | Generation orchestration, pawn capture, prompts, portrait files and texture caches | `PortraitRuntime.cs`, `PawnCapture.cs`, `PortraitPrompt.cs`, `PromptLibrary.cs`, `PortraitData.cs` |
| `Providers/` | Provider contract, registry, and shared request helpers | `PortraitGenerationProvider.cs`, `PortraitProviderUtilities.cs`, `PortraitImageCaptions.cs`, `PortraitInpaintMask.cs` |
| `Providers/OpenAI/`, `Google/`, `NAI/`, `Comfy/` | Each service's requests, responses, and service-specific image handling | `*PortraitProvider.cs`; NovelAI also has `NovelAiReferenceImage.cs` and `NovelAiInpaintComposite.cs` |
| `Settings/` | Saved settings, migrations, packaged defaults | `Settings.cs`, `SettingsDefaults.cs`, `ProviderOptionDefaults.cs`, `RimAIPortraitMod.PromptDefaults.cs` |
| `TagExtractor/` | Optional image-to-tag requests and response validation | `TagExtraction.cs` |
| `RemoveBG/` | Background color removal and pixel transforms | `BackgroundRemoval.cs` |

## How the modules connect

1. `RimAIPortraitMod` loads packaged defaults and saved settings, applies prompt defaults, installs Harmony patches, and creates the persistent `PortraitRuntime` component.
2. `PortraitGizmoPatch` opens the gallery. Settings and dialogs collect user input; `InspectPanePatch` draws the selected portrait during RimWorld UI rendering.
3. `PortraitRuntime` validates the selected Base or Expression provider, captures the pawn image through `PawnCapture`, builds prompts through `PortraitPrompt`, and prepares the provider request. Optional tag extraction runs through `TagExtraction` before generation.
4. The provider registry returns an `IPortraitGenerationProvider`. Its coroutine sends the request with `UnityWebRequest` and returns image bytes or an error. Provider-specific HTTP and payload logic stays in that provider's folder.
5. `PortraitRuntime` saves successful results through `PortraitData`. `PortraitData` owns portrait paths, gallery files, active state images, death and grayscale textures, and game-scoped texture caches. The UI reads those images for display.

Base and Expression provider settings are independent. The runtime chooses the Base provider for `PortraitState.Normal` and the Expression provider for other states. `PortraitGenerationProvider.cs` defines the shared request, result, configuration types, interface, and registry; `PortraitProviderUtilities.cs` contains common HTTP, MIME, parsing, and style-file helpers. `PortraitImageCaptions.cs` reads packaged image captions, and `PortraitInpaintMask.cs` prepares the ComfyUI mask. NovelAI's inpaint composite and reference-image handling remain under `Providers/NAI/`.

## Where to make a change

| Change | Primary files | Also check |
| --- | --- | --- |
| Add or change a provider | `Providers/<Name>/`, `Providers/PortraitGenerationProvider.cs` | `Settings/Settings.cs`, `GUI/ProviderSettingsUi.cs`, packaged defaults, both language files; see [provider-structure.md](provider-structure.md) |
| Change generation flow or provider selection | `Portraits/PortraitRuntime.cs` | Provider contract, relevant dialogs, `TagExtractor/TagExtraction.cs` if tags are involved |
| Change prompts or add a portrait state | `Portraits/PortraitPrompt.cs`, `Portraits/PromptLibrary.cs`, `Resources/Prompts/` | `Settings/Settings.cs`, `Settings/RimAIPortraitMod.PromptDefaults.cs`, `GUI/PromptSettingsUi.cs`, both language files |
| Change saved settings or defaults | `Settings/Settings.cs`, `Settings/SettingsDefaults.cs`, `Settings/ProviderOptionDefaults.cs` | `Resources/Json/`, settings UI, existing Scribe keys and migrations |
| Change portrait storage or assignment | `Portraits/PortraitData.cs` | Gallery dialog, inspect-pane rendering, legacy paths and cache invalidation |
| Change background removal | `RemoveBG/BackgroundRemoval.cs` | `GUI/Dialog_BackgroundRemoval.cs`, `Portraits/PortraitRuntime.cs` for batch work |
| Change tag extraction | `TagExtractor/TagExtraction.cs` | `Portraits/PortraitRuntime.cs`, `Resources/Prompts/TagExtractor*.md`, settings and provider support |
| Change a screen or user-facing message | Relevant `GUI/` file | `Localization.cs` and matching keys in English and Korean keyed XML |

## File and runtime boundaries

- Keep GUI drawing lightweight, especially in `InspectPanePatch.cs`. Long work belongs in `PortraitRuntime` coroutines; pixel transforms belong in `RemoveBG/` or the relevant provider helper.
- Keep provider implementations focused on generating image bytes. Gallery behavior, portrait file naming, assignment, and texture caches belong to `PortraitData` and the UI.
- Preserve Scribe keys, pawn folder names, portrait filenames, and old-file migrations when changing persistence. Portraits are stored in RimWorld save-data storage, outside the packaged mod.
- When replacing cached or temporary `Texture2D` objects, destroy the old instances. `PortraitData` must clear its static caches when the current game changes.
- Use `Localization.T(...)` for UI text and errors, with matching entries in `Resources/Languages/English/Keyed/RimAIPortrait.xml` and `Resources/Languages/Korean/Keyed/RimAIPortrait.xml`.

`Resources/About/`, `Languages/`, `Prompts/`, `Json/`, `Style/`, `Help/`, and `Textures/` contain packaged data. `Scripts/build.ps1` builds the assembly and copies those resources into `dist/RimAIPortrait/`. Moving a C# file between source folders does not move saved portraits or change the shared namespace. Compile Release after code changes; for documentation-only edits, check paths and links.
