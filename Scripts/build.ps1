[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$RimWorldDir = "D:\SteamLibrary\steamapps\common\RimWorld",

    [string]$HarmonyDll,

    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "Source\RimAIPortrait\RimAIPortrait.csproj"
$resourcesRoot = Join-Path $repositoryRoot "Resources"
$distRoot = Join-Path $repositoryRoot "dist"
$buildOutput = Join-Path $distRoot ".build\bin\1.7\$Configuration"
$assemblyPath = Join-Path $buildOutput "RimAIPortrait.dll"
$preserved16Assemblies = Join-Path $repositoryRoot "Compatibility\1.6\Assemblies"
$preserved16Dll = Join-Path $preserved16Assemblies "RimAIPortrait.dll"
$stageDirectory = Join-Path $distRoot ".build\package-1.7"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $distRoot "RimAIPortrait"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$distPrefix = [System.IO.Path]::GetFullPath($distRoot).TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
$buildRoot = [System.IO.Path]::GetFullPath((Join-Path $distRoot ".build"))
$buildPrefix = $buildRoot.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
if ((-not $OutputDirectory.StartsWith($distPrefix, [System.StringComparison]::OrdinalIgnoreCase)) -or
    $OutputDirectory.Equals($buildRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $OutputDirectory.StartsWith($buildPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be inside $distRoot and outside its .build directory."
}
if (-not (Test-Path -LiteralPath $preserved16Dll -PathType Leaf)) {
    throw "The preserved 1.6 DLL is missing: $preserved16Dll"
}
$expected16Hash = (Get-Content -LiteralPath (Join-Path $PSScriptRoot "preserved-1.6.sha256") -TotalCount 1).Trim()
if ((Get-FileHash -LiteralPath $preserved16Dll -Algorithm SHA256).Hash -ne $expected16Hash) {
    throw "The preserved 1.6 DLL does not match Scripts/preserved-1.6.sha256."
}

if ([string]::IsNullOrWhiteSpace($RimWorldDir)) {
    throw "RimWorldDir is required. Set RIMWORLD_DIR or pass -RimWorldDir."
}

$RimWorldDir = [System.IO.Path]::GetFullPath($RimWorldDir)
$versionFile = Join-Path $RimWorldDir "Version.txt"
$rimWorldVersion = if (Test-Path -LiteralPath $versionFile -PathType Leaf) {
    Get-Content -LiteralPath $versionFile -TotalCount 1
} else { "" }
if ($rimWorldVersion -notmatch '^1\.7(?:\.|\s|$)') {
    throw "RimWorldDir must be a 1.7 installation; found '$rimWorldVersion': $RimWorldDir"
}
foreach ($name in @("Assembly-CSharp.dll", "UnityStubs.dll", "Waylith.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $RimWorldDir $name) -PathType Leaf)) {
        throw "RimWorldDir is missing $name`: $RimWorldDir"
    }
}

if ([string]::IsNullOrWhiteSpace($HarmonyDll)) {
    $steamAppsRoot = Split-Path (Split-Path $RimWorldDir -Parent) -Parent
    $harmonyRoots = @(
        (Join-Path $RimWorldDir "Mods\Harmony"),
        (Join-Path $steamAppsRoot "workshop\content\294100\2009463077")
    )
    $harmonyVersions = @("1.7", "Current")
    foreach ($harmonyRoot in $harmonyRoots) {
        foreach ($harmonyVersion in $harmonyVersions) {
            $candidate = Join-Path $harmonyRoot "$harmonyVersion\Assemblies\0Harmony.dll"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $HarmonyDll = $candidate
                break
            }
        }
        if ($HarmonyDll) {
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($HarmonyDll) -or -not (Test-Path -LiteralPath $HarmonyDll -PathType Leaf)) {
    throw "Harmony DLL was not found. Install Harmony for this RimWorld version or pass -HarmonyDll: $HarmonyDll"
}
$HarmonyDll = [System.IO.Path]::GetFullPath($HarmonyDll)

$buildArguments = @(
    "build",
    $projectPath,
    "--configuration", $Configuration,
    "--output", $buildOutput,
    "--property:RimWorldVersion=1.7",
    "--property:RimWorldDir=$RimWorldDir",
    "--property:HarmonyDll=$HarmonyDll"
)

Write-Host "Building Rim AI Expressive Portraits for 1.7 ($Configuration)..."
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "The build succeeded but the expected assembly was not found: $assemblyPath"
}

if (Test-Path -LiteralPath $stageDirectory) {
    Remove-Item -LiteralPath $stageDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDirectory | Out-Null

foreach ($directory in @("About", "Languages", "Prompts", "Json", "Textures", "Style", "Help", "Notice")) {
    $resourceDirectory = Join-Path $resourcesRoot $directory
    if (-not (Test-Path -LiteralPath $resourceDirectory -PathType Container)) {
        throw "Required resource directory was not found: $resourceDirectory"
    }
    Copy-Item -LiteralPath $resourceDirectory -Destination $stageDirectory -Recurse
}
$loadFolders = Join-Path $resourcesRoot "LoadFolders.xml"
if (-not (Test-Path -LiteralPath $loadFolders -PathType Leaf)) {
    throw "Required LoadFolders.xml was not found: $loadFolders"
}
Copy-Item -LiteralPath $loadFolders -Destination $stageDirectory

# RimWorld uses this exact filename for the in-game and Steam Workshop preview.
$previewJpg = Join-Path $resourcesRoot "About\Preview.jpg"
$previewPng = Join-Path $stageDirectory "About\Preview.png"
if (-not (Test-Path -LiteralPath $previewJpg -PathType Leaf)) {
    throw "Required preview image was not found: $previewJpg"
}

if (Test-Path -LiteralPath $previewPng -PathType Leaf) {
    Remove-Item -LiteralPath $previewPng -Force
}
Copy-Item -LiteralPath $previewJpg -Destination $previewPng

$dist16Assemblies = Join-Path $stageDirectory "1.6\Assemblies"
$dist17Assemblies = Join-Path $stageDirectory "1.7\Assemblies"
New-Item -ItemType Directory -Path $dist16Assemblies,$dist17Assemblies | Out-Null
Copy-Item -LiteralPath $preserved16Dll -Destination $dist16Assemblies
$preserved16Pdb = Join-Path $preserved16Assemblies "RimAIPortrait.pdb"
if (Test-Path -LiteralPath $preserved16Pdb -PathType Leaf) {
    Copy-Item -LiteralPath $preserved16Pdb -Destination $dist16Assemblies
}
Copy-Item -LiteralPath $assemblyPath -Destination $dist17Assemblies

$pdbPath = [System.IO.Path]::ChangeExtension($assemblyPath, ".pdb")
if (Test-Path -LiteralPath $pdbPath -PathType Leaf) {
    Copy-Item -LiteralPath $pdbPath -Destination $dist17Assemblies
}

if ((Get-FileHash -LiteralPath $preserved16Dll -Algorithm SHA256).Hash -ne
    (Get-FileHash -LiteralPath (Join-Path $dist16Assemblies "RimAIPortrait.dll") -Algorithm SHA256).Hash) {
    throw "The staged 1.6 DLL differs from the preserved build."
}
if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path (Split-Path $OutputDirectory -Parent) -Force | Out-Null
Move-Item -LiteralPath $stageDirectory -Destination $OutputDirectory

Write-Host "Build complete: $OutputDirectory"
