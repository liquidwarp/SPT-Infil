param(
    [string]$SPTPath = 'E:\Games\SPT 4.1',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$InstallMod,

    [switch]$HotReload,

    [switch]$PackageRelease
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path "$SPTPath\EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll")) {
    throw "SPT not found at '$SPTPath'"
}

if ($HotReload -and -not $InstallMod) {
    throw "-HotReload requires -InstallMod"
}

$buildArgs = @(
    "$PSScriptRoot\Infil.slnx"
    "-c", $Configuration
    "-p:SPTPath=$SPTPath"
    "-p:InstallMod=$($InstallMod.IsPresent)"
    "-p:HotReload=$($HotReload.IsPresent)"
    "-p:PackageRelease=$($PackageRelease.IsPresent)"
    "-tlp:verbosity=detailed"
)
dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
