using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Game.Spawning;
using JsonType;
using UnityEngine;

namespace Liquidwarp.SpawnSelector.Spawn;

public class SpawnSpot
{
    public string Label;
    public Vector3 Position;
    public HashSet<string> PointIds;
}

public static class SpawnSpots
{
    private const float DefaultMaxSpread = 200f;

    private static readonly Dictionary<string, float> MaxSpreadByLocation = new(StringComparer.OrdinalIgnoreCase)
    {
        ["factory4_day"] = 75f,
        ["factory4_night"] = 75f,
        ["laboratory"] = 75f,
        ["labyrinth"] = 75f,
        ["Sandbox"] = 150f,
        ["Sandbox_high"] = 150f,
    };

    private static readonly Dictionary<(string, EPlayerSide), List<SpawnSpot>> Cache = new();

    public static List<SpawnSpot> For(LocationSettings.Location location, EPlayerSide side)
    {
        if (!Cache.TryGetValue((location.Id, side), out var spots))
            Cache[(location.Id, side)] = spots = Build(location, side);
        return spots;
    }

    private static List<SpawnSpot> Build(LocationSettings.Location location, EPlayerSide side)
    {
        var points = (location.SpawnPointParams ?? [])
            .Where(point => point.Categories.ContainPlayerCategory() && point.Sides.Contain(side) && point.Position != null)
            .ToList();

        var maxSpread = MaxSpreadByLocation.TryGetValue(location.Id, out var spread) ? spread : DefaultMaxSpread;

        return Cluster(points.Select(point => (Vector3)point.Position).ToList(), maxSpread)
            .Select((members, index) => new SpawnSpot
            {
                Label = $"#{index + 1}",
                Position = members.Aggregate(Vector3.zero, (sum, i) => sum + (Vector3)points[i].Position) / members.Count,
                PointIds = new HashSet<string>(members.Select(i => points[i].Id)),
            })
            .ToList();
    }

    /// Complete-linkage clustering
    private static List<List<int>> Cluster(List<Vector3> positions, float maxSpread)
    {
        var clusters = positions.Select((_, i) => new List<int> { i }).ToList();
        var distances = new float[clusters.Count, clusters.Count];
        for (var i = 0; i < clusters.Count; i++)
            for (var j = i + 1; j < clusters.Count; j++)
                distances[i, j] = distances[j, i] = HorizontalDistance(positions[i], positions[j]);

        var active = Enumerable.Range(0, clusters.Count).ToList();
        while (true)
        {
            var (best, a, b) = (float.MaxValue, -1, -1);
            for (var x = 0; x < active.Count; x++)
                for (var y = x + 1; y < active.Count; y++)
                    if (distances[active[x], active[y]] < best)
                        (best, a, b) = (distances[active[x], active[y]], active[x], active[y]);

            if (a < 0 || best > maxSpread)
                break;

            clusters[a].AddRange(clusters[b]);
            active.Remove(b);
            foreach (var k in active)
                distances[a, k] = distances[k, a] = Mathf.Max(distances[a, k], distances[b, k]);
        }

        return active.Select(i => clusters[i]).ToList();
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
}
