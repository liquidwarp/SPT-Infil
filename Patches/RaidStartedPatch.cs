using System.Reflection;
using EFT;
using HarmonyLib;
using Liquidwarp.Infil.Spawn;
using SPT.Reflection.Patching;

namespace Liquidwarp.Infil.Patches;

public class RaidStartedPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));

    [PatchPostfix]
    public static void Postfix(GameWorld __instance)
    {
        if (SpawnChoiceStore.Unreached is { } unreached)
        {
            if (SpawnChoiceStore.IsFor(__instance.LocationId))
                Plugin.LogSource.LogWarning($"{unreached} was never applied: the spawn was chosen without Infil");
            else
                Plugin.LogSource.LogInfo($"Dropping {unreached}: this game is on {__instance.LocationId}");
        }

        SpawnChoiceStore.Clear();
    }
}
