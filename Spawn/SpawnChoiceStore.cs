using System;
using EFT;
using Fika.Core.Main.GameMode;

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

    public static SpawnSpot Get(BaseGameController controller)
    {
        var raidSettings = controller?.RaidSettings;
        return raidSettings == null || raidSettings.isInTransition ? null : Get(controller.Location?.Id, raidSettings.Side);
    }

    public static void Set(string locationId, ESideType side, SpawnSpot spot)
    {
        _locationId = locationId;
        _side = side;
        _spot = spot;
    }

    public static void Clear() => _spot = null;
}
