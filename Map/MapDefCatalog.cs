namespace Liquidwarp.Infil.Map;

public static class MapDefCatalog
{
    public static IMapPack Find(string locationId) => MapMods.Find(locationId);
}
