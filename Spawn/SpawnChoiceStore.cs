using System;
using Comfort.Common;
using EFT;

namespace Liquidwarp.Infil.Spawn;

public static class SpawnChoiceStore
{
    private static string _locationId;
    private static ESideType _side;
    private static SpawnSpot _spot;

    public static SpawnSpot Get(string locationId, ESideType side) =>
        _spot != null && side == _side && SameMap(locationId, _locationId) ? _spot : null;

    public static SpawnSpot Get(EPlayerSide side) =>
        Get(Singleton<GameWorld>.Instance?.LocationId, side == EPlayerSide.Savage ? ESideType.Savage : ESideType.Pmc);

    public static void Set(string locationId, ESideType side, SpawnSpot spot)
    {
        _locationId = locationId;
        _side = side;
        _spot = spot;
    }

    public static void Clear() => _spot = null;

    // Variants of one map (Ground Zero for low and high levels, Factory day and night) share their spawn points
    private static bool SameMap(string locationId, string otherLocationId)
    {
        if (locationId == null || otherLocationId == null)
            return false;

        if (string.Equals(locationId, otherLocationId, StringComparison.OrdinalIgnoreCase))
            return true;

        var mapDef = MapDefCatalog.Find(locationId);
        return mapDef != null && mapDef == MapDefCatalog.Find(otherLocationId);
    }
}
