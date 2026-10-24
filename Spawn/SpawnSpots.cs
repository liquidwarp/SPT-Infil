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

    private static readonly Dictionary<(string, EPlayerSide), (object MapDef, SpawnPointParams[] Source, List<SpawnSpot> Spots)> Cache = new();

    public static List<SpawnSpot> For(LocationSettings.Location location, EPlayerSide side, MapDef mapDef)
    {
        var source = location.SpawnPointParams ?? [];
        var key = (location.Id, side);
        if (Cache.TryGetValue(key, out var cached) && ReferenceEquals(cached.MapDef, mapDef) && SameSpawnPoints(cached.Source, source))
            return cached.Spots;

        var maxSpread = MaxSpreadByLocation.TryGetValue(location.Id, out var spread) ? spread : DefaultMaxSpread;
        var spots = Build(source, side, mapDef, maxSpread);
        Cache[key] = (mapDef, source, spots);
        return spots;
    }

    private static bool SameSpawnPoints(SpawnPointParams[] a, SpawnPointParams[] b) =>
        a == b || a.Select(point => point.Id).SequenceEqual(b.Select(point => point.Id));

    private static List<SpawnSpot> Build(SpawnPointParams[] source, EPlayerSide side, MapDef mapDef, float maxSpread)
    {
        var points = source
            .Where(point => point.Categories.ContainPlayerCategory() && point.Sides.Contain(side) && point.Position != null)
            .ToList();
        var positions = points.Select(point => (Vector3)point.Position).ToList();

        var spots = new List<SpawnSpot>();
        foreach (var members in Cluster(positions, maxSpread))
        {
            var memberPositions = members.Select(i => positions[i]).ToList();
            var position = memberPositions.Aggregate(Vector3.zero, (sum, p) => sum + p) / memberPositions.Count;
            spots.Add(new SpawnSpot
            {
                Label = $"#{spots.Count + 1}",
                Position = position,
                Where = mapDef != null ? MapDefCatalog.DescribeWhere(mapDef, MathUtils.ConvertToMapPosition(position)) : null,
                PointIds = new HashSet<string>(members.Select(i => points[i].Id)),
                PointPositions = memberPositions,
            });
        }

        return spots;
    }

    /// Complete-linkage clustering
    private static List<List<int>> Cluster(List<Vector3> positions, float maxSpread)
    {
        var count = positions.Count;
        var clusters = positions.Select((_, i) => new List<int> { i }).ToList();
        var distances = new float[count, count];
        for (var i = 0; i < count; i++)
            for (var j = i + 1; j < count; j++)
                distances[i, j] = distances[j, i] = HorizontalDistance(positions[i], positions[j]);

        var active = Enumerable.Repeat(true, count).ToArray();
        var nearest = Enumerable.Range(0, count).Select(i => Nearest(distances, active, i)).ToArray();
        while (true)
        {
            var a = -1;
            for (var i = 0; i < count; i++)
                if (active[i] && nearest[i] >= 0 && (a < 0 || distances[i, nearest[i]] < distances[a, nearest[a]]))
                    a = i;

            if (a < 0 || distances[a, nearest[a]] > maxSpread)
                break;

            // Merging only ever grows distances: only rows that pointed at the merged pair need a new nearest
            var b = nearest[a];
            clusters[a].AddRange(clusters[b]);
            active[b] = false;
            for (var k = 0; k < count; k++)
                if (active[k] && k != a)
                    distances[a, k] = distances[k, a] = Mathf.Max(distances[a, k], distances[b, k]);

            for (var k = 0; k < count; k++)
                if (active[k] && (k == a || nearest[k] == a || nearest[k] == b))
                    nearest[k] = Nearest(distances, active, k);
        }

        return Enumerable.Range(0, count).Where(i => active[i]).Select(i => clusters[i]).ToList();
    }

    private static int Nearest(float[,] distances, bool[] active, int i)
    {
        var nearest = -1;
        for (var j = 0; j < active.Length; j++)
            if (active[j] && j != i && (nearest < 0 || distances[i, j] < distances[i, nearest]))
                nearest = j;

        return nearest;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
}
