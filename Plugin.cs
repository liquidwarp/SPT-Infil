using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Liquidwarp.SpawnSelector.Patches;

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

    private void Awake()
    {
        LogSource = Logger;
        FikaLoaded = Chainloader.PluginInfos.ContainsKey(FikaGuid);

        MapDefCatalog.Preload();

        new SelectSpawnPointPatch().Enable();
        new NextButtonPatch().Enable();
        new RaidStartedPatch().Enable();

        if (FikaLoaded)
            EnableFikaPatches();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnableFikaPatches() => new ReceiveSpawnPointPatch().Enable();
}
