using BepInEx;
using BepInEx.Bootstrap;
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
[BepInDependency(FikaGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(MapVariantsMaps.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.mpstark.dynamicmaps", MyModInfo.DYNAMIC_MAPS_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private const string FikaGuid = "com.fika.core";

    public static ManualLogSource LogSource;
    public static bool FikaLoaded { get; private set; }

    private PatchManager _patchManager;

    private void Awake()
    {
        LogSource = Logger;
        FikaLoaded = Chainloader.PluginInfos.ContainsKey(FikaGuid);
        MapVariantsMaps.Preload();

        _patchManager = new PatchManager(this);
        _patchManager.AddPatch(new SelectSpawnPointPatch());
        _patchManager.AddPatch(new NextScreenPatch());
        _patchManager.AddPatch(new ReadyScreenPatch());
        _patchManager.AddPatch(new ConfirmationScreenPatch());
        _patchManager.AddPatch(new RaidStartedPatch());

        if (FikaLoaded && SpawnTogetherPatch.Create() is { } fikaPatch)
            _patchManager.AddPatch(fikaPatch);

        _patchManager.EnablePatches();
    }

    private void OnDestroy()
    {
        SpawnPickerWindow.CloseOpen();
        SpawnPickerWindow.CloseCover();
        ConfirmationScreenPatch.RemoveText();
        _patchManager?.DisablePatches();
    }
}
