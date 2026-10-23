using EFT;

namespace Liquidwarp.Infil.Spawn;

public static class SpawnChoiceStore
{
    private static ESideType _side;
    private static SpawnSpot _spot;

    public static SpawnSpot Get(ESideType side) => side == _side ? _spot : null;

    public static SpawnSpot Get(EPlayerSide side) => Get(side == EPlayerSide.Savage ? ESideType.Savage : ESideType.Pmc);

    public static void Set(ESideType side, SpawnSpot spot)
    {
        _side = side;
        _spot = spot;
    }

    public static void Clear() => _spot = null;
}
