#Requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$SPTPath,
    [Parameter(Mandatory)][string]$ClientAssembly,
    [switch]$WithFika
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run this test with Windows PowerShell (powershell.exe) for the installed HarmonyX runtime.' }

$flags = [Reflection.BindingFlags]'Instance, Public, NonPublic'
$declared = $flags -bor [Reflection.BindingFlags]::DeclaredOnly
function Assert-Target($patch, [Reflection.MethodInfo]$expected) {
    if ($null -eq $patch -or $null -eq $expected) { throw "Missing patch or expected method: $patch / $expected" }
    $actual = $patch.GetType().GetMethod('GetTargetMethod', $flags).Invoke($patch, $null)
    if (-not $expected.Equals($actual) -or $actual.ReflectedType -ne $expected.ReflectedType -or $actual.ContainsGenericParameters) {
        throw "$($patch.GetType().Name) resolved '$actual' on '$($actual.ReflectedType)', expected '$expected' on '$($expected.ReflectedType)'"
    }
    Write-Output "PASS  $($patch.GetType().Name) targets $($expected.DeclaringType.Name).$($expected.Name)"
}

foreach ($dll in @(
    'EscapeFromTarkov_Data\Managed\netstandard.dll', 'EscapeFromTarkov_Data\Managed\UnityEngine.dll',
    'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll', 'BepInEx\core\0Harmony.dll',
    'BepInEx\core\BepInEx.dll', 'BepInEx\plugins\spt\spt-reflection.dll'
)) {
    $null = [Reflection.Assembly]::LoadFrom((Join-Path $SPTPath $dll))
}
$null = [Reflection.Assembly]::LoadFrom((Get-Item -LiteralPath $ClientAssembly).FullName)
$fikaPath = Join-Path $SPTPath 'BepInEx\plugins\Fika\Fika.Core.dll'

# Chainloader initializes its config when its plugin registry is first read.
$config = [IO.Path]::GetTempFileName()
try {
    [BepInEx.Paths].GetProperty('BepInExConfigPath').SetValue($null, $config)
    if ($WithFika) {
        $fika = [Reflection.Assembly]::LoadFrom($fikaPath)
        [BepInEx.Bootstrap.Chainloader]::PluginInfos.Add('com.fika.core', [BepInEx.PluginInfo]::new())
        [Liquidwarp.Infil.Plugin]::LogSource = [BepInEx.Logging.ManualLogSource]::new('Infil.Tests')
        $patch = [Liquidwarp.Infil.Compat.FikaClientSpawnPatch]::Create()
        $controller = $fika.GetType('Fika.Core.Main.GameMode.ClientGameController', $true)
        $target = $controller.GetMethod('CheckSpawnTogether', $flags, $null, [type[]]@(), $null)
        Assert-Target $patch $target
        $getter = $patch.GetType().GetField('_getRaidSettings', 'Static, NonPublic').GetValue($null)
        if ($target.ReturnType -ne [bool] -or $getter.ReturnType -ne [EFT.RaidSettings] -or
            $getter.MethodHandle -ne $controller.GetProperty('RaidSettings').GetGetMethod().MethodHandle) {
            throw 'Unexpected Fika group-spawn return type or raid settings getter'
        }
        return
    }
    if ($null -ne [Liquidwarp.Infil.Compat.FikaClientSpawnPatch]::Create()) { throw 'Infil created a Fika patch with no registered Fika plugin' }
    Write-Output 'PASS  No group-spawn patch when Fika is absent'
} finally {
    [IO.File]::Delete($config)
}

$controller = [EFT.UI.Matchmaker.MatchMakerSelectionLocationScreen+SelectionLocationScreenController]
foreach ($ready in $false, $true) {
    $name = if ($ready) { 'ShowReadyScreen' } else { 'ShowNextScreen' }
    # Map Variants patches the MethodInfo reflected from the closed declaring base.
    $base = $controller.GetMethod($name, $flags).DeclaringType
    Assert-Target ([Liquidwarp.Infil.Patches.AdvanceScreenPatch]::new($ready)) ($base.GetMethod($name, $declared, $null, [type[]]@(), $null))
}
Assert-Target ([Liquidwarp.Infil.Patches.RaidStartedPatch]::new()) ([EFT.GameWorld].GetMethod('OnGameStarted', $declared, $null, [type[]]@(), $null))
$show = [EFT.UI.Matchmaker.MatchMakerAcceptScreen].GetMethod('Show', $declared, $null, [type[]]@([EFT.IEftSession], [EFT.RaidSettings], [EFT.RaidSettings]), $null)
Assert-Target ([Liquidwarp.Infil.Patches.ConfirmationScreenPatch]::new()) $show

$spawnSystem = [EFT.Game.Spawning.SpawnSystem]
$interface = [EFT.Game.Spawning.ISpawnSystem]
$map = $spawnSystem.GetInterfaceMap($interface)
$select = $map.TargetMethods[[Array]::IndexOf($map.InterfaceMethods, $interface.GetMethod('SelectSpawnPoint'))]
$patch = [Liquidwarp.Infil.Patches.SelectSpawnPointPatch]::new()
Assert-Target $patch $select
$arguments = $patch.GetType().GetMethod('Postfix').GetParameters()
foreach ($required in '__instance', 'category', 'side', 'person', 'profileId', '__result') {
    if ($arguments.Name -cnotcontains $required) { throw "Spawn postfix does not receive '$required'" }
}
foreach ($arg in $arguments) {
    $expectedType = switch -CaseSensitive ($arg.Name) {
        '__instance' { $spawnSystem }
        '__result'   { $select.ReturnType.MakeByRefType() }
        default      { ($select.GetParameters() | Where-Object Name -CEQ $arg.Name).ParameterType }
    }
    if ($arg.ParameterType -ne $expectedType) { throw "Cannot bind spawn postfix parameter '$arg' to '$select'" }
}

# FikaCompat records the plugin registry once per process, as it does in the game.
if (Test-Path -LiteralPath $fikaPath -PathType Leaf) {
    & "$PSHOME\powershell.exe" -NoProfile -NonInteractive -File $PSCommandPath -SPTPath $SPTPath -ClientAssembly $ClientAssembly -WithFika
    if ($LASTEXITCODE -ne 0) { throw "Installed Fika patch check failed ($LASTEXITCODE)" }
} else {
    Write-Output 'SKIP  Fika is not installed'
}
