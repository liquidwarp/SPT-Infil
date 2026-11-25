using System.Collections.Generic;
using UnityEngine;

namespace Liquidwarp.Infil.Map;

public static class MapWhere
{
    private static readonly string[] Directions =
        ["east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east"];

    public static string Describe(Vector2 boundsMin, Vector2 boundsMax, float coordinateRotation,
        IEnumerable<(string Text, Vector3 Position)> labels, Vector3 mapPosition)
    {
        var landmark = FindLandmark(labels, mapPosition);
        if (landmark != null)
            return $"near {landmark}";

        var direction = FindDirection(boundsMin, boundsMax, coordinateRotation, mapPosition);
        return direction != null ? $"in the {direction}" : null;
    }

    private static string FindLandmark(IEnumerable<(string Text, Vector3 Position)> labels, Vector3 mapPosition)
    {
        string nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var (text, position) in labels)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var distance = Vector2.Distance(position, mapPosition);
            if (distance < nearestDistance)
                (nearest, nearestDistance) = (text, distance);
        }

        return nearest?.Replace('\n', ' ').Trim();
    }

    private static string FindDirection(Vector2 boundsMin, Vector2 boundsMax, float coordinateRotation, Vector3 mapPosition)
    {
        var center = MapMath.GetMidpoint(boundsMin, boundsMax);
        var halfSize = (boundsMax - boundsMin) / 2f;
        if (Mathf.Approximately(halfSize.x, 0f) || Mathf.Approximately(halfSize.y, 0f))
            return null;

        var offset = new Vector2((mapPosition.x - center.x) / Mathf.Abs(halfSize.x), (mapPosition.y - center.y) / Mathf.Abs(halfSize.y));
        if (offset.magnitude < 0.33f)
            return "middle";

        // Map packs rotate so north is up
        var shown = MapMath.GetRotatedVector2(offset, coordinateRotation);
        var sector = Mathf.RoundToInt(Mathf.Atan2(shown.y, shown.x) * Mathf.Rad2Deg / 45f);
        return Directions[(sector % 8 + 8) % 8];
    }
}
