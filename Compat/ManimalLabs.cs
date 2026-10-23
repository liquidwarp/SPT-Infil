using System;
using BepInEx.Bootstrap;

namespace Liquidwarp.Infil.Compat;

public static class ManimalLabs
{
    private const string Guid = "com.manimal.labsboiler";
    private const string LocationId = "laboratory";

    private static readonly bool Loaded = Chainloader.PluginInfos.ContainsKey(Guid);

    public static bool Replaces(string locationId) =>
        Loaded && string.Equals(locationId, LocationId, StringComparison.OrdinalIgnoreCase);
}
