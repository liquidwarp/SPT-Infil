using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using Liquidwarp.Infil.Compat;
using Liquidwarp.Infil.Patches;
using Liquidwarp.Infil.UI;
using SPT.Reflection.Patching;

namespace Liquidwarp.Infil;

// NOTE: No class layout (base type, interfaces, fields) in this mod may use a type from other mods directly!
//
// Fika and MapVariants may be absent, and a future DynamicMaps update may remove a type: a class whose layout uses a
// missing type throws when loaded. Unity's JSON converter scan loads every class. It would fail, leaving the game without
// its default JSON settings, causing catastrophical issues downstream.
//
// Lambdas, closures and locals in async methods count, the compiler turning them into fields.
//
// Method signatures and bodies may use DynamicMaps types: the scan never resolves them, and a missing type only fails the
// call, which the picker's catches handle. Fika and MapVariants are only reached by name, through reflection.
//
// build.ps1 enforces this rule on the built assembly (check-load-safety.ps1).

[BepInPlugin(MyModInfo.MOD_GUID, MyModInfo.MOD_NAME, MyModInfo.MOD_VERSION)]
[BepInDependency("com.SPT.custom", MyModInfo.CLIENT_SPT_VERSION)]
[BepInDependency(FikaCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(MapVariantsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.mpstark.dynamicmaps", MyModInfo.DYNAMIC_MAPS_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static ManualLogSource LogSource;

    private readonly List<ModulePatch> _patches = [];

    private void Awake()
    {
        LogSource = Logger;
        MapVariantsCompat.Preload();

        Enable(new SelectSpawnPointPatch());
        Enable(new AdvanceScreenPatch(ready: false));
        Enable(new AdvanceScreenPatch(ready: true));
        Enable(new ConfirmationScreenPatch());
        Enable(new RaidStartedPatch());

        if (FikaClientSpawnPatch.Create() is { } fikaPatch)
            FikaClientSpawnPatch.Enabled = Enable(fikaPatch);
    }

    private bool Enable(ModulePatch patch)
    {
        try
        {
            patch.Enable();
            _patches.Add(patch);
            return true;
        }
        catch (Exception ex)
        {
            LogSource.LogError($"Could not enable {patch.GetType().Name}: {ex}");
            return false;
        }
    }

    private void OnDestroy()
    {
        SpawnPickerWindow.CloseOpen();
        SpawnPickerWindow.CloseCover();
        ConfirmationScreenPatch.RemoveText();
        foreach (var patch in _patches)
            patch.Disable();
    }
}
