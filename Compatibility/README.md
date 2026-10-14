# Preserved RimWorld 1.6 assembly

`1.6/Assemblies/RimAIPortrait.dll` is the frozen release built before the project switched to RimWorld 1.7. It is tracked so a fresh checkout can package both game versions without a local backup. The PDB is kept beside it for debugging.

`Scripts/build.ps1` checks the DLL against `Scripts/preserved-1.6.sha256`, builds only the 1.7 assembly, and copies both versioned assemblies into `dist/RimAIPortrait/`.
