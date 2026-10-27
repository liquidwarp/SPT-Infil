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
            if (!IsOwnProfile(profileId))
                return;

            var spot = SpawnChoiceStore.GetForRaid(side);
            if (spot == null)
                return;

            SpawnChoiceStore.MarkReached();
            var points = __instance._spawnPoints.Where(p => spot.PointIds.Contains(p.Id)).ToList();
            if (points.Count == 0)
            {
                Plugin.LogSource.LogWarning($"Chosen spawn {spot.Label} does not exist on this map: keeping the default spawn");
                return;
            }

            __result = points.Contains(__result) ? __result : SafestPoint(__instance, points, side, person);
            Plugin.LogSource.LogInfo($"Spawning at {spot.Label}: point {__result.Id} {__result.Position}");
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not apply the spawn choice: {ex}");
        }
    }

    private static bool IsOwnProfile(string profileId)
    {
        var session = PatchConstants.BackEndSession;
        return profileId != null && session != null && (profileId == session.Profile?.Id || profileId == session.ProfileOfPet?.Id);
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
