using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace Liquidwarp.Infil.Compat;

public static class MapVariantsMaps
{
    private const string Guid = "com.lennoxp90.mapvariants";

    private static HashSet<string> _managed;

    public static bool Manages(string locationId)
    {
        if (locationId == null || !Chainloader.PluginInfos.ContainsKey(Guid))
            return false;

        _managed ??= Load();
        return _managed != null && _managed.Contains(locationId);
    }

    private static HashSet<string> Load()
    {
        try
        {
            var json = RequestHandler.GetJson("/mapvariants/managed");
            var ids = JObject.Parse(json)["locationIds"]?.ToObject<string[]>() ?? [];
            return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogWarning($"Could not ask MapVariants which maps it manages: {ex.Message}");
            return null;
        }
    }
}
