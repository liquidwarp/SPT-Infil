using System;
using EFT;

namespace Liquidwarp.SpawnSelector.Spawn;

public static class SpawnChoiceStore
{
    private static string _locationId;
    private static ESideType _side;
    private static SpawnSpot _spot;

    public static SpawnSpot Get(string locationId, ESideType side) =>
        _spot != null && side == _side && string.Equals(locationId, _locationId, StringComparison.OrdinalIgnoreCase)
            ? _spot
            : null;

    public static SpawnSpot Get(EPlayerSide side) =>
        _spot != null && _side == (side == EPlayerSide.Savage ? ESideType.Savage : ESideType.Pmc) ? _spot : null;

    public static void Set(string locationId, ESideType side, SpawnSpot spot)
    {
        _locationId = locationId;
        _side = side;
        _spot = spot;
    }

    public static void Clear() => _spot = null;
}
