[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$RimWorldDir = "D:\SteamLibrary\steamapps\common\RimWorld",

    [string]$HarmonyDll = "D:\SteamLibrary\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll",

    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "Source\RimAIPortrait\RimAIPortrait.csproj"
$resourcesRoot = Join-Path $repositoryRoot "Resources"
$distRoot = Join-Path $repositoryRoot "dist"
$buildOutput = Join-Path $distRoot ".build\bin\$Configuration"
$assemblyPath = Join-Path $buildOutput "RimAIPortrait.dll"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $distRoot "RimAIPortrait"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

if ([string]::IsNullOrWhiteSpace($RimWorldDir)) {
    throw "RimWorldDir is required. Set RIMWORLD_DIR or pass -RimWorldDir."
}

$RimWorldDir = [System.IO.Path]::GetFullPath($RimWorldDir)
if (-not (Test-Path -LiteralPath (Join-Path $RimWorldDir "RimWorldWin64_Data\Managed\Assembly-CSharp.dll") -PathType Leaf)) {
    throw "RimWorldDir does not contain RimWorldWin64_Data\Managed\Assembly-CSharp.dll: $RimWorldDir"
}

if ([string]::IsNullOrWhiteSpace($HarmonyDll)) {
    $HarmonyDll = Join-Path $RimWorldDir "Mods\Harmony\Current\Assemblies\0Harmony.dll"
}

$HarmonyDll = [System.IO.Path]::GetFullPath($HarmonyDll)
if (-not (Test-Path -LiteralPath $HarmonyDll -PathType Leaf)) {
    throw "Harmony DLL was not found. Install Harmony or pass -HarmonyDll: $HarmonyDll"
}

$buildArguments = @(
    "build",
    $projectPath,
    "--configuration", $Configuration,
    "--output", $buildOutput,
    "--property:RimWorldDir=$RimWorldDir",
    "--property:HarmonyDll=$HarmonyDll"
)

Write-Host "Building Rim AI Expressive Portraits ($Configuration)..."
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "The build succeeded but the expected assembly was not found: $assemblyPath"
}

# Only allow automatic cleanup inside this repository's dist directory.
$distPrefix = [System.IO.Path]::GetFullPath($distRoot).TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
if (-not $OutputDirectory.StartsWith($distPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be inside $distRoot"
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null

foreach ($directory in @("About", "Languages", "Prompts", "Textures")) {
    $resourceDirectory = Join-Path $resourcesRoot $directory
    if (-not (Test-Path -LiteralPath $resourceDirectory -PathType Container)) {
        throw "Required resource directory was not found: $resourceDirectory"
    }
    Copy-Item -LiteralPath $resourceDirectory -Destination $OutputDirectory -Recurse
}

$distAssemblies = Join-Path $OutputDirectory "Assemblies"
New-Item -ItemType Directory -Path $distAssemblies | Out-Null
Copy-Item -LiteralPath $assemblyPath -Destination $distAssemblies

$pdbPath = [System.IO.Path]::ChangeExtension($assemblyPath, ".pdb")
if (Test-Path -LiteralPath $pdbPath -PathType Leaf) {
    Copy-Item -LiteralPath $pdbPath -Destination $distAssemblies
}

Write-Host "Build complete: $OutputDirectory"
