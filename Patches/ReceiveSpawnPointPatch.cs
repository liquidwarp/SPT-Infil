using System;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using Fika.Core.Main.GameMode;
using HarmonyLib;
using Liquidwarp.SpawnSelector.Spawn;
using SPT.Reflection.Patching;

namespace Liquidwarp.SpawnSelector.Patches;
public class ReceiveSpawnPointPatch : ModulePatch
{
    private static readonly Func<ClientGameController, bool> CheckSpawnTogether =
        AccessTools.MethodDelegate<Func<ClientGameController, bool>>(AccessTools.Method(typeof(ClientGameController), "CheckSpawnTogether"));

    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(ClientGameController), nameof(ClientGameController.ReceiveSpawnPoint));

    [PatchPrefix]
    public static bool Prefix(ClientGameController __instance, Profile profile, ref Task __result)
    {
        try
        {
            var spot = SpawnChoiceStore.Get(__instance);
            if (spot == null || !CheckSpawnTogether(__instance))
                return true;

            __instance.CreateSpawnSystem(profile);
            if (__instance.SpawnPoint != null && spot.PointIds.Contains(__instance.SpawnPoint.Id))
            {
                __result = Task.CompletedTask;
                return false;
            }

            // The pick didn't apply: spawn as Fika would, which waits for the host's infiltration point
            __instance.InfiltrationPoint = null;
            return true;
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not apply the spawn choice: {ex}");
            return true;
        }
    }
}
