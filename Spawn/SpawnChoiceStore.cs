using System;
using Comfort.Common;
using EFT;

namespace Liquidwarp.Infil.Spawn;

public static class SpawnChoiceStore
{
    private static string _locationId;
    private static ESideType _side;
    private static SpawnSpot _spot;
    private static bool _reached;

    public static SpawnSpot Get(string locationId, ESideType side) => side == _side && IsFor(locationId) ? _spot : null;

    public static SpawnSpot GetForRaid(EPlayerSide side) => GetForRaid(side == EPlayerSide.Savage ? ESideType.Savage : ESideType.Pmc);

    public static SpawnSpot GetForRaid(ESideType side) => Get(Singleton<GameWorld>.Instance?.LocationId, side);

    public static bool IsFor(string locationId) => string.Equals(MapKey(locationId), MapKey(_locationId), StringComparison.OrdinalIgnoreCase);

    private static string MapKey(string locationId) =>
        string.Equals(locationId, "Sandbox_high", StringComparison.OrdinalIgnoreCase) ? "Sandbox" : locationId;

    public static void MarkReached() => _reached = true;

    public static string Unreached => _spot != null && !_reached ? $"{_spot.Title} chosen for {_locationId} ({_side})" : null;

    public static void Set(string locationId, ESideType side, SpawnSpot spot)
    {
        _locationId = locationId;
        _side = side;
        _spot = spot;
        _reached = false;
    }

    public static void Clear() => _spot = null;
}
