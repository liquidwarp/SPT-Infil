using BepInEx.Bootstrap;

namespace Liquidwarp.Infil.Map;

public static class MapMods
{
    public const string DynamicMapsGuid = "com.mpstark.dynamicmaps";
    public const string MapsExpandedGuid = "com.dark.mapsexpanded";

    public static bool MapsExpandedLoaded => Chainloader.PluginInfos.ContainsKey(MapsExpandedGuid);
    public static bool DynamicMapsLoaded => Chainloader.PluginInfos.ContainsKey(DynamicMapsGuid);
    public static bool AnyLoaded => MapsExpandedLoaded || DynamicMapsLoaded;

    public static IMapPack Find(string locationId)
    {
        // Prefer Maps Expanded when present; the two map mods are mutually incompatible.
        if (MapsExpandedLoaded)
            return MapsExpandedPack.Find(locationId);

        if (DynamicMapsLoaded)
            return DynamicMapsPack.Find(locationId);

        return null;
    }
}
