# Repository Guidelines

## Project Structure & Module Organization

This repository is a RimWorld mod. C# implementation files live in `Source/RimAIPortrait/`; the project targets .NET Framework 4.7.2 and C# 7.3. Runtime inputs are grouped under `Resources/`: mod metadata in `About/`, textures in `Textures/AIExpressivePortraits/`, prompts in `Prompts/`, and translations in `Languages/`. Generated packages belong in `dist/`.

Keep gameplay patches focused: Harmony entry points belong in `*Patch.cs`, portrait generation and provider logic in `PortraitRuntime.cs`, and settings UI or persistence in `Settings.cs` and `RimAIPortraitMod.cs`.

## Build, Test, and Development Commands

Set `RIMWORLD_DIR` to a RimWorld installation containing the managed game assemblies, then run:

```powershell
.\scripts\build.ps1
```

The script compiles the DLL, then combines it with `Resources/` in `dist/RimAIPortrait/`. Set `RIMWORLD_DIR` first; if Harmony is installed elsewhere, pass `-HarmonyDll "C:\path\to\0Harmony.dll"`. Use `-Configuration Debug` for a development build. Install or link the generated package into RimWorld's `Mods` directory and enable Harmony before this mod.

## Coding Style & Naming Conventions

Follow the existing C# style: four-space indentation, Allman braces, `PascalCase` for types and methods, and `camelCase` for locals and private fields. Keep namespaces under `RimAIPortrait`. Use explicit types where they improve clarity and remain compatible with C# 7.3. Add user-facing text through keyed localization XML rather than embedding new strings; keep English and Korean key sets synchronized. Name prompt files after their `PortraitState` value, such as `Resources/Prompts/Angry.md`.

## Testing Guidelines

There is currently no automated test project or coverage requirement. Every change must compile in Release mode. Manually verify settings persistence, portrait generation for each affected provider, gallery/preview behavior, and Harmony-patched UI in supported RimWorld versions. Check `Player.log` for exceptions and warnings.

## Commit & Pull Request Guidelines

Use short, imperative commit subjects, for example `Fix portrait cache lookup`. Keep unrelated changes separate. Pull requests should explain user-visible behavior, list build and manual-test results, link relevant issues, and include screenshots for settings, gallery, or portrait-layout changes. Never commit API keys, local workflow paths, generated portraits, or machine-specific RimWorld paths.
