using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DynamicMaps.Data;
using DynamicMaps.Utils;
using UnityEngine;

namespace Liquidwarp.Infil;

public static class MapDefCatalog
{
    private static Task<Dictionary<string, MapDef>> _mapDefs;

    public static void Preload() => _mapDefs = Task.Run(Load);

    public static MapDef Find(string locationId) =>
        _mapDefs.Result.TryGetValue(locationId, out var mapDef) ? mapDef : null;

    private static readonly string[] Directions = ["east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east"];

    public static string DescribeWhere(MapDef mapDef, Vector3 mapPosition)
    {
        var landmark = FindLandmark(mapDef, mapPosition);
        if (landmark != null)
            return $"near {landmark}";

        var direction = FindDirection(mapDef, mapPosition);
        return direction != null ? $"in the {direction}" : null;
    }

    private static string FindLandmark(MapDef mapDef, Vector3 mapPosition)
    {
        var nearest = mapDef.Labels?
            .Where(label => !string.IsNullOrWhiteSpace(label.Text))
            .OrderBy(label => Vector2.Distance(label.Position, mapPosition))
            .FirstOrDefault();

        return nearest?.Text.Replace('\n', ' ').Trim();
    }

    private static string FindDirection(MapDef mapDef, Vector3 mapPosition)
    {
        if (mapDef.Bounds == null)
            return null;

        var center = MathUtils.GetMidpoint(mapDef.Bounds.Min, mapDef.Bounds.Max);
        var halfSize = (mapDef.Bounds.Max - mapDef.Bounds.Min) / 2f;
        if (Mathf.Approximately(halfSize.x, 0f) || Mathf.Approximately(halfSize.y, 0f))
            return null;

        var offset = new Vector2((mapPosition.x - center.x) / Mathf.Abs(halfSize.x), (mapPosition.y - center.y) / Mathf.Abs(halfSize.y));
        if (offset.magnitude < 0.33f)
            return "middle";

        // DynamicMaps rotates the map so that north is up
        var shown = MathUtils.GetRotatedVector2(offset, mapDef.CoordinateRotation);
        var sector = Mathf.RoundToInt(Mathf.Atan2(shown.y, shown.x) * Mathf.Rad2Deg / 45f);
        return Directions[(sector % 8 + 8) % 8];
    }

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
