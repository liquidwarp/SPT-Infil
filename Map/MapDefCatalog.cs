using System;
using System.Linq;
using Comfort.Common;
using DynamicMaps.Data;
using DynamicMaps.UI.Controls;
using DynamicMaps.Utils;
using EFT.UI;
using UnityEngine;

namespace Liquidwarp.Infil.Map;

public static class MapDefCatalog
{
    // The map DynamicMaps shows in raid for this location
    public static MapDef Find(string locationId)
    {
        var plugin = DynamicMaps.Plugin.Instance;
        if (locationId == null || plugin == null)
            return null;

        var mapScreen = plugin.Map;
        if (mapScreen == null)
            return null;

        var dropdown = mapScreen.GetComponentInChildren<MapSelectDropdown>(true);
        if (dropdown == null)
            return null;

        var screen = mapScreen.transform;
        var parent = screen.parent;
        var wasActive = mapScreen.gameObject.activeSelf;
        screen.SetParent(Singleton<CommonUI>.Instance.transform, false);
        mapScreen.gameObject.SetActive(true);

        object selected = null;
        Action<MapDef> capture = mapDef => selected = mapDef;
        dropdown.OnMapSelected += capture;
        try
        {
            dropdown.LoadPreferredMap(locationId);
        }
        finally
        {
            dropdown.OnMapSelected -= capture;
            mapScreen.gameObject.SetActive(wasActive);
            screen.SetParent(parent, false);
        }

        var map = selected as MapDef;
        return map != null && dropdown.GetMapDefs().Contains(map) ? map : null;
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
