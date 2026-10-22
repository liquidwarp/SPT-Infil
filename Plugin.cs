using BepInEx;
using BepInEx.Logging;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Liquidwarp.SpawnSelector.Patches;
using Liquidwarp.SpawnSelector.Spawn;

namespace Liquidwarp.SpawnSelector;

[BepInPlugin(MyModInfo.MOD_GUID, MyModInfo.MOD_NAME, MyModInfo.MOD_VERSION)]
[BepInDependency("com.SPT.custom", MyModInfo.CLIENT_SPT_VERSION)]
[BepInDependency("com.fika.core")]
[BepInDependency("com.mpstark.dynamicmaps")]
public class Plugin : BaseUnityPlugin
{
    public static ManualLogSource LogSource;

    private void Awake()
    {
        LogSource = Logger;

        MapDefCatalog.Preload();

        new SelectSpawnPointPatch().Enable();
        new ReceiveSpawnPointPatch().Enable();
        new NextButtonPatch().Enable();

        FikaEventDispatcher.SubscribeEvent<FikaRaidStartedEvent>(_ => SpawnChoiceStore.Clear());
    }
}
