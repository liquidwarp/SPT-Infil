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
    public static void Postfix() => SpawnChoiceStore.Clear();
}
