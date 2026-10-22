using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DynamicMaps.Data;

namespace Liquidwarp.SpawnSelector;

public static class MapDefCatalog
{
    private static Task<Dictionary<string, MapDef>> _mapDefs;

    public static void Preload() => _mapDefs = Task.Run(Load);

    public static MapDef Find(string locationId) =>
        _mapDefs.Result.TryGetValue(locationId, out var mapDef) ? mapDef : null;

    private static Dictionary<string, MapDef> Load()
    {
        var mapDefs = new Dictionary<string, MapDef>(StringComparer.OrdinalIgnoreCase);
        var mapsPath = Path.Combine(DynamicMaps.Plugin.Path, "Maps");

        try
        {
            foreach (var file in Directory.EnumerateFiles(mapsPath, "*.json*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension != ".json" && extension != ".jsonc")
                    continue;

                var mapDef = MapDef.LoadFromPath(file);
                if (mapDef == null)
                    continue;

                foreach (var name in mapDef.MapInternalNames)
                    mapDefs.TryAdd(name, mapDef);
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not load DynamicMaps map definitions from {mapsPath}: {ex.Message}");
        }

        return mapDefs;
    }
}
