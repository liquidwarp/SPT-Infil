using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Liquidwarp.SpawnSelector.Patches;
using Liquidwarp.SpawnSelector.UI;
using SPT.Reflection.Patching;

namespace Liquidwarp.SpawnSelector;

[BepInPlugin(MyModInfo.MOD_GUID, MyModInfo.MOD_NAME, MyModInfo.MOD_VERSION)]
[BepInDependency("com.SPT.custom", MyModInfo.CLIENT_SPT_VERSION)]
[BepInDependency(FikaGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.mpstark.dynamicmaps")]
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

        MapDefCatalog.Preload();

        _patchManager = new PatchManager(this);
        _patchManager.AddPatch(new SelectSpawnPointPatch());
        _patchManager.AddPatch(new NextButtonPatch());
        _patchManager.AddPatch(new ConfirmationScreenPatch());
        _patchManager.AddPatch(new RaidStartedPatch());

        if (FikaLoaded)
            AddFikaPatches();

        _patchManager.EnablePatches();
    }

    private void OnDestroy()
    {
        SpawnPickerWindow.CloseOpen();
        ConfirmationScreenPatch.RemoveText();
        _patchManager?.DisablePatches();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddFikaPatches() => _patchManager.AddPatch(new ReceiveSpawnPointPatch());
}
