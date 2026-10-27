using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using Liquidwarp.Infil.Spawn;
using SPT.Reflection.Patching;

namespace Liquidwarp.Infil.Compat;

/// A Fika client who picked a spawn spawns there, even in a raid set to spawn together.
/// Fika's members are found by name, so Infil doesn't reference Fika.
public class FikaClientSpawnPatch : ModulePatch
{
    private const string ControllerType = "Fika.Core.Main.GameMode.ClientGameController";

    private static MethodInfo _target;
    private static MethodInfo _getRaidSettings;

    public static bool Enabled { get; internal set; }

    public static ModulePatch Create()
    {
        if (!FikaCompat.Loaded)
            return null;

        var controller = AccessTools.TypeByName(ControllerType);
        _target = AccessTools.Method(controller, "CheckSpawnTogether", []);
        _getRaidSettings = AccessTools.PropertyGetter(controller, "RaidSettings");
        if (_target?.ReturnType == typeof(bool) && _getRaidSettings != null)
            return new FikaClientSpawnPatch();

        Plugin.LogSource.LogWarning("This Fika version's ClientGameController lacks a member Infil uses: in a raid you join, "
            + "you will spawn at the host's location when it is set to spawn together");
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
            if (_getRaidSettings.Invoke(__instance, null) is RaidSettings raidSettings && SpawnChoiceStore.GetForRaid(raidSettings.Side) != null)
                __result = false;
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not apply the spawn choice: {ex}");
        }
    }
}
