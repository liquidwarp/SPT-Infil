using System;
using System.Collections.Generic;
using System.Linq;
using DynamicMaps.Data;
using DynamicMaps.Utils;
using EFT;
using EFT.Game.Spawning;
using JsonType;
using Liquidwarp.Infil.Map;
using UnityEngine;

namespace Liquidwarp.Infil.Spawn;

public class SpawnSpot
{
    public string Label;
    public string Title => $"Spawn area {Label}";
    public Vector3 Position;
    public string Where;
    public HashSet<string> PointIds;
    public List<Vector3> PointPositions;
}

public static class SpawnSpots
{
    private const float DefaultMaxSpread = 200f;

    private static readonly Dictionary<string, float> MaxSpreadByLocation = new(StringComparer.OrdinalIgnoreCase)
    {
        ["factory4_day"] = 50f,
        ["factory4_night"] = 50f,
        ["laboratory"] = 30f,
        ["labyrinth"] = 25f,
        ["RezervBase"] = 125f,
        ["Lighthouse"] = 125f,
        ["Sandbox"] = 100f,
        ["Sandbox_high"] = 100f,
    };

    private static readonly Dictionary<(string, EPlayerSide), (SpawnPointParams[] Source, MapDef MapDef, List<SpawnSpot> Spots)> Cache = new();

    public static List<SpawnSpot> For(LocationSettings.Location location, EPlayerSide side, MapDef mapDef)
    {
        var source = location.SpawnPointParams ?? [];
        if (Cache.TryGetValue((location.Id, side), out var cached) && cached.MapDef == mapDef && SameSpawnPoints(cached.Source, source))
            return cached.Spots;

        var spots = Build(location, source, side, mapDef);
        Cache[(location.Id, side)] = (source, mapDef, spots);
        return spots;
    }

    private static bool SameSpawnPoints(SpawnPointParams[] a, SpawnPointParams[] b) =>
        a == b || a.Select(point => point.Id).SequenceEqual(b.Select(point => point.Id));

    private static List<SpawnSpot> Build(LocationSettings.Location location, SpawnPointParams[] source, EPlayerSide side, MapDef mapDef)
    {
        var points = source
            .Where(point => point.Categories.ContainPlayerCategory() && point.Sides.Contain(side) && point.Position != null)
            .ToList();
        var positions = points.Select(point => (Vector3)point.Position).ToList();

        var maxSpread = MaxSpreadByLocation.TryGetValue(location.Id, out var spread) ? spread : DefaultMaxSpread;

        return Cluster(positions, maxSpread)
            .Select((members, index) =>
            {
                var memberPositions = members.Select(i => positions[i]).ToList();
                var position = memberPositions.Aggregate(Vector3.zero, (sum, p) => sum + p) / memberPositions.Count;
                return new SpawnSpot
                {
                    Label = $"#{index + 1}",
                    Position = position,
                    Where = mapDef != null ? MapDefCatalog.DescribeWhere(mapDef, MathUtils.ConvertToMapPosition(position)) : null,
                    PointIds = new HashSet<string>(members.Select(i => points[i].Id)),
                    PointPositions = memberPositions,
                };
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
