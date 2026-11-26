using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.Game.Spawning;
using Liquidwarp.Infil.Spawn;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;

namespace Liquidwarp.Infil.Patches;

public class SelectSpawnPointPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        var map = typeof(SpawnSystem).GetInterfaceMap(typeof(ISpawnSystem));
        return map.TargetMethods[Array.IndexOf(map.InterfaceMethods, typeof(ISpawnSystem).GetMethod(nameof(ISpawnSystem.SelectSpawnPoint)))];
    }

    [PatchPostfix]
    public static void Postfix(SpawnSystem __instance, ESpawnCategory category, EPlayerSide side, IPlayer person, string profileId, ref ISpawnPoint __result)
    {
        if (category != ESpawnCategory.Player)
            return;

        try
        {
            Apply(__instance, side, person, profileId, PatchConstants.BackEndSession, ref __result);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not apply the spawn choice: {ex}");
        }
    }

    internal static void Apply(SpawnSystem spawnSystem, EPlayerSide side, IPlayer person, string profileId, IEftSession session, ref ISpawnPoint result)
    {
        if (profileId == null || session == null || (profileId != session.Profile?.Id && profileId != session.ProfileOfPet?.Id))
            return;

        var spot = SpawnChoiceStore.GetForRaid(side);
        if (spot == null)
            return;

        SpawnChoiceStore.MarkReached();
        var points = spawnSystem._spawnPoints.Where(p => spot.PointIds.Contains(p.Id)).ToList();
        if (points.Count == 0)
        {
            Plugin.LogSource.LogWarning($"Chosen spawn {spot.Label} does not exist on this map: keeping the default spawn");
            return;
        }

        result = points.Contains(result) ? result : SafestPoint(spawnSystem, points, side, person);
        Plugin.LogSource.LogInfo($"Spawning at {spot.Label}: point {result.Id} {result.Position}");
    }

    private static ISpawnPoint SafestPoint(SpawnSystem spawnSystem, List<ISpawnPoint> points, EPlayerSide side, IPlayer person)
    {
        var usable = points.Where(p => !p.SpawnBlocked && p.Sides.Contain(side)).ToList();
        if (usable.Count > 0)
            points = usable;

        var others = spawnSystem._players.Except(person).ToList();
        return points.FarthestFromPlayersOnDistanceWithRandom(others, spawnSystem._settings.SpawnSafeDistanceSqr) ?? points.RandomElement();
    }
}
