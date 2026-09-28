# Repository Guidelines

## Project Overview

This repository builds **RIM AI Expressive Portraits**, a Harmony-based RimWorld 1.5/1.6 mod. The code targets .NET Framework 4.7.2 and C# 7.3; do not introduce newer language or runtime APIs. The mod captures pawn data, builds prompts, calls OpenAI, Google, or a local ComfyUI server through Unity coroutines, stores portraits outside the mod package, and draws the selected portrait in RimWorld's inspect UI.

## Repository Layout and Responsibilities

- `Source/RimAIPortrait/` contains the C# project. See `MODULE.md` for folder boundaries; keep the `RimAIPortrait` namespace when moving files.
- `Portraits/PortraitRuntime.cs` coordinates generation and owns the persistent `MonoBehaviour`; `Portraits/PawnCapture.cs` captures pawn images. Provider-specific HTTP and payload code belongs under `Providers/OpenAI/`, `Providers/Google/`, `Providers/NAI/`, or `Providers/Comfy/`.
- `Providers/PortraitGenerationProvider.cs` defines provider requests, results, configuration objects, the interface, and registry. Shared parsing, MIME, style-image, and `UnityWebRequest` helpers belong in `Providers/PortraitProviderUtilities.cs`.
- `Portraits/PortraitPrompt.cs` builds pawn prompts. `Portraits/PromptLibrary.cs` defines `PortraitState` and compiled fallback prompts. Packaged defaults in `Resources/Prompts/` override those fallbacks at startup.
- `Portraits/PortraitData.cs` owns portrait paths, gallery files, per-game caches, active state images, and generated death/grayscale textures. Preserve its game-scope cache invalidation and destroy replaced `Texture2D` instances.
- `Settings/Settings.cs` contains persisted fields, Scribe keys/defaults, and migrations. `Settings/SettingsDefaults.cs`, `Settings/ProviderOptionDefaults.cs`, and `Settings/RimAIPortraitMod.PromptDefaults.cs` load packaged defaults. `RimAIPortraitMod.cs` initializes the mod; `GUI/RimAIPortraitMod.SettingsUi.cs`, `GUI/ProviderSettingsUi.cs`, and `GUI/PromptSettingsUi.cs` render settings.
- `GUI/InspectPanePatch.cs` draws portraits from `UIRootOnGUI`; keep work there lightweight because it executes during GUI rendering. `GUI/PortraitGizmoPatch.cs` adds the pawn command. Dialog behavior belongs in `GUI/Dialog_*.cs`; pixel transforms belong in `RemoveBG/BackgroundRemoval.cs`.
- `Resources/About/`, `Languages/`, `Prompts/`, `Json/`, and `Textures/` are copied into the distributable mod. `Scripts/build.ps1` builds and packages them into `dist/RimAIPortrait/`. `Agents/` contains task prompts, not runtime code.

## Build and Validation

The build needs RimWorld managed assemblies and Harmony. The script currently has developer-machine defaults, so pass local paths explicitly on other machines:

```powershell
.\Scripts\build.ps1 `
  -Configuration Release `
  -RimWorldDir "C:\path\to\RimWorld" `
  -HarmonyDll "C:\path\to\0Harmony.dll"
```

Use `-Configuration Debug` only for development. The script runs `dotnet build`, validates inputs, removes and recreates the selected package directory (which must be inside `dist/`), copies all required resources, and places the DLL/PDB under `Assemblies/`. There is no automated test project. Every code change must at least compile in Release; documentation/resource-only changes should be checked for path, XML, and key consistency.

For manual testing, install or link `dist/RimAIPortrait/` into RimWorld's `Mods` directory with Harmony loaded first. Exercise the affected base and expression providers, settings save/reload, gallery assignment/regeneration/deletion, preview/background removal, inspect-pane layout and state switching, then inspect `Player.log` for warnings or exceptions. Never make real paid API calls merely to validate an unrelated change.

## Change Coupling

- Adding a `PortraitState` requires updating its prompt file, `PromptLibrary.State`, settings field and `ExposeData` Scribe entry, `IsStateEnabled`, `PortraitPrompt.StateOverride`, settings UI, and both English and Korean localization keys. Keep the state filename exactly `<PortraitState>.md`.
- Adding a provider requires updating `AiProvider`, persisted base/expression settings and migrations as needed, `ProviderSettingsUi`, a configuration type and `IPortraitGenerationProvider` implementation, and `PortraitProviderRegistry` registration. Base and expression configurations are intentionally independent.
- User-facing UI/status/error text must use `Localization.T(...)` and matching keys in both `Resources/Languages/English/Keyed/RimAIPortrait.xml` and `Resources/Languages/Korean/Keyed/RimAIPortrait.xml`. Prompt section labels sent to models are not RimWorld UI strings.
- Changes to saved field names, portrait filenames, or pawn-folder naming need backward-compatible loading/migration. Portrait files live in RimWorld save-data storage, not under `Resources/` or `dist/`.
- Provider requests must remain coroutine-based and use `UnityWebRequest`; do not block RimWorld's main thread. Bound reference-image counts, preserve timeouts and useful localized HTTP errors, and never log API keys or full sensitive payloads.

## NovelAI Sleep Inpaint Decision (Test8 013–021)

The current single-rectangle experiment is documented in `Test8/Results/021/README.md`. Reusing the successful strength-0.7 responses for `Base.png` and `Base2.png`, a 512×512 face crop enlarged to 1024×1024 closed both pairs of eyes. Applying each response through one user rectangle with a 4px inward feather left a visible horizontal color boundary on Base and misaligned eyebrow/hair lines on Base2. Both composites preserved every pixel outside the user rectangle. Experiment 020 used the same Base2 input and request mask at strength 0.6; its eyes stayed open and the irises became pale. Experiment 019 sent a blurred elliptical mask and received a gray band in the raw response.

**Decision:** keep the NovelAI Sleep default strength at 0.7. Do not ship the focused-crop plus single-rectangle composite as a finished fix: it succeeds at closing eyes in these two samples but fails the visual boundary check in both. Do not use a blurred ellipse as the API mask. Do not infer that a wider feather or a larger final rectangle fixes mismatched content inside the mask. The existing two-region Sleep composition remains available while a one-rectangle solution is developed; do not silently replace the user's single rectangle with two required selections.

For the next implementation candidate, keep one user-drawn rectangle in `GUI/Dialog_NovelAiInpaint.cs`. In `Providers/NAI/NovelAiPortraitProvider.cs`, derive one **binary, padded rectangular request mask** from it and evaluate a crop with unmasked context enlarged to about 1 MP. Keep the request mask and final composition region separate in `Providers/NAI/NovelAiInpaintComposite.cs`; never composite the entire padded request region merely because it was sent to NovelAI. Preserve source dimensions and map the response back before composing. Treat this crop path as an approximation of the web UI, not an exact replica. Before enabling it by default, show the same final single-rectangle result on both Base images without a visible boundary or doubled hair/eye lines, verify that Sleep remains closed, verify exact pixel preservation outside the user rectangle, and compile Release. If that visual gate fails, retain the existing runtime path rather than presenting the experiment as a solved seam issue. Do not make another paid request just to repeat inputs and seed already stored in Test8.

## Coding Conventions

Match the existing namespace and style: `RimAIPortrait`, four-space indentation, Allman braces, `PascalCase` types/methods/properties, and `camelCase` locals/private fields. Prefer clear explicit types while remaining C# 7.3 compatible. Keep Harmony patches narrow and provider-neutral orchestration out of provider implementations. When creating Unity textures, explicitly destroy temporary, replaced, and window-owned textures; static caches must be cleared when the current game changes.

Avoid drive-by reformatting and unrelated cleanup. Some files contain compact legacy statements and generated-looking XML documentation; improve them only when the requested change touches that code. Do not commit API keys, provider responses, generated portraits, local workflow files, machine-specific RimWorld/Harmony paths, build output, or save-data artifacts.

## Commits and Pull Requests

Use short imperative commit subjects such as `Fix portrait cache lookup`. Keep unrelated changes separate. Pull requests should describe user-visible behavior, note provider/state compatibility, list Release build and manual-test results, link relevant issues, and include screenshots for settings, gallery, preview, or portrait-layout changes.
