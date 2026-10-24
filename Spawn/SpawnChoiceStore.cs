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
        side == _side && string.Equals(locationId, _locationId, StringComparison.OrdinalIgnoreCase) ? _spot : null;

    public static SpawnSpot GetForRaid(EPlayerSide side) =>
        Get(Singleton<GameWorld>.Instance?.LocationId, side == EPlayerSide.Savage ? ESideType.Savage : ESideType.Pmc);

    public static void Set(string locationId, ESideType side, SpawnSpot spot)
    {
        _locationId = locationId;
        _side = side;
        _spot = spot;
    }

    public static void Clear() => _spot = null;
}
