param(
    [string]$SPTPath = 'D:\SPT\4.1 Fika',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$InstallMod,

    [switch]$HotReload,

    [switch]$PackageRelease,

    [switch]$Test
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path "$SPTPath\EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll")) {
    throw "SPT not found at '$SPTPath'"
}
$SPTPath = (Resolve-Path -LiteralPath $SPTPath).Path

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
    "-p:CheckLoadSafety=true"
    "-tlp:verbosity=detailed"
)
dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }

if ($Test) {
    # The solution build above already compiled and checked the plugin.
    dotnet test "$PSScriptRoot\Tests\Infil.Tests.csproj" -c $Configuration "-p:SPTPath=$SPTPath" -p:BuildProjectReferences=false
    if ($LASTEXITCODE -ne 0) { throw "Infil tests failed ($LASTEXITCODE)" }

    # The game's HarmonyX needs the desktop CLR for its method lookup.
    $clientAssembly = Join-Path $PSScriptRoot "bin\$Configuration\netstandard2.1\Liquidwarp-Infil.dll"
    powershell.exe -NoProfile -NonInteractive -File "$PSScriptRoot\Tests\Test-PatchTargets.ps1" -SPTPath $SPTPath -ClientAssembly $clientAssembly
    if ($LASTEXITCODE -ne 0) { throw "Patch target checks failed ($LASTEXITCODE)" }
}
