using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using Fika.Core.Main.GameMode;
using Liquidwarp.SpawnSelector.Spawn;
using SPT.Reflection.Patching;

namespace Liquidwarp.SpawnSelector.Patches;

public class SelectSpawnPointPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var map = typeof(SpawnSystem).GetInterfaceMap(typeof(ISpawnSystem));
        return map.TargetMethods[Array.IndexOf(map.InterfaceMethods, typeof(ISpawnSystem).GetMethod(nameof(ISpawnSystem.SelectSpawnPoint)))];
    }

    [PatchPostfix]
    public static void Postfix(SpawnSystem __instance, ESpawnCategory category, EPlayerSide side, IPlayer person, ref ISpawnPoint __result)
    {
        if (category != ESpawnCategory.Player)
            return;

        try
        {
            var spot = SpawnChoiceStore.Get(Singleton<IFikaGame>.Instance?.GameController);
            if (spot == null)
                return;

            var points = __instance._spawnPoints.Where(p => spot.PointIds.Contains(p.Id)).ToList();
            if (points.Count == 0)
            {
                Plugin.LogSource.LogWarning($"Chosen spawn {spot.Label} does not exist on this map: keeping the default spawn");
                return;
            }

            var point = points.Contains(__result) ? __result : SafestPoint(__instance, points, side, person);
            __result = point;
            Plugin.LogSource.LogInfo($"Spawning at {spot.Label}: point {point.Id} {point.Position}");
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not apply the spawn choice: {ex}");
        }
    }

    private static ISpawnPoint SafestPoint(SpawnSystem spawnSystem, List<ISpawnPoint> points, EPlayerSide side, IPlayer person)
    {
        var usable = points.Where(p => !p.SpawnBlocked && p.Sides.Contain(side)).ToList();
        if (usable.Count > 0)
            points = usable;

        var others = spawnSystem._players.Where(p => p != person).Select(p => p.Position).ToList();
        var safe = points.Where(p => others.All(o => (o - p.Position).sqrMagnitude >= spawnSystem._settings.SpawnSafeDistanceSqr)).ToList();
        return safe.Count > 0
            ? safe.RandomElement()
            : points.OrderByDescending(p => others.Min(o => (o - p.Position).sqrMagnitude)).First();
    }
}
