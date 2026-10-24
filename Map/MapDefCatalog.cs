using System;
using System.Linq;
using System.Reflection;
using DynamicMaps.Data;
using DynamicMaps.UI;
using DynamicMaps.UI.Controls;
using DynamicMaps.Utils;
using HarmonyLib;
using UnityEngine;

namespace Liquidwarp.Infil.Map;

public static class MapDefCatalog
{
    private const string DropdownField = "_mapSelectDropdown";

    private static readonly FieldInfo Dropdown = FindDropdown();

    private static FieldInfo FindDropdown()
    {
        var field = AccessTools.Field(typeof(ModdedMapScreen), DropdownField);
        if (field == null)
            Plugin.LogSource.LogWarning($"DynamicMaps has no {nameof(ModdedMapScreen)}.{DropdownField} to list its maps: no map will offer spawn choices");
        return field;
    }

    public static MapDef Find(string locationId)
    {
        var mapScreen = DynamicMaps.Plugin.Instance?.Map;
        if (locationId == null || mapScreen == null || Dropdown?.GetValue(mapScreen) is not MapSelectDropdown dropdown)
            return null;

        MapDef found = null;
        foreach (var mapDef in dropdown.GetMapDefs())
        {
            if (mapDef.MapInternalNames.Contains(locationId, StringComparer.OrdinalIgnoreCase)
                && (found == null || string.Compare(mapDef.DisplayName, found.DisplayName, StringComparison.CurrentCulture) < 0))
                found = mapDef;
        }

        return found;
    }

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
        string nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var label in mapDef.Labels ?? [])
        {
            if (string.IsNullOrWhiteSpace(label.Text))
                continue;

            var distance = Vector2.Distance(label.Position, mapPosition);
            if (distance < nearestDistance)
                (nearest, nearestDistance) = (label.Text, distance);
        }

        return nearest?.Replace('\n', ' ').Trim();
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
}
