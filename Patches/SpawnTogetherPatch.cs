using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using Liquidwarp.Infil.Spawn;
using SPT.Reflection.Patching;

namespace Liquidwarp.Infil.Patches;

/// Lets a Fika group member who picked a spawn use it instead of spawning with the host.
/// Fika then spawns them as when spawning apart, through the spawn system SelectSpawnPointPatch patches.
/// IMPORTANT: Fika's members are found by name, so Infil doesn't reference Fika.
public class SpawnTogetherPatch : ModulePatch
{
    private const string ControllerType = "Fika.Core.Main.GameMode.ClientGameController";

    private static MethodInfo _target;
    private static MethodInfo _getRaidSettings;

    public static ModulePatch Create()
    {
        var controller = AccessTools.TypeByName(ControllerType);
        _target = controller != null ? AccessTools.Method(controller, "CheckSpawnTogether", []) : null;
        _getRaidSettings = controller != null ? AccessTools.PropertyGetter(controller, "RaidSettings") : null;
        if (_target?.ReturnType == typeof(bool) && _getRaidSettings != null)
            return new SpawnTogetherPatch();

        Plugin.LogSource.LogWarning($"This Fika version's {ControllerType} lacks CheckSpawnTogether() or RaidSettings: Fika groups will spawn as Fika chooses");
        return null;
    }

    protected override MethodBase GetTargetMethod() => _target;

    [PatchPostfix]
    public static void Postfix(object __instance, ref bool __result)
    {
        if (!__result)
            return;

        try
        {
            if (_getRaidSettings.Invoke(__instance, null) is RaidSettings raidSettings
                && SpawnChoiceStore.Get(raidSettings.SelectedLocation?.Id, raidSettings.Side) != null)
                __result = false;
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not apply the spawn choice: {ex}");
        }
    }
}
