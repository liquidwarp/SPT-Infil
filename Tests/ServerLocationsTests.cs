using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EFT;
using EFT.Game.Spawning;
using Liquidwarp.Infil.Spawn;
using NUnit.Framework;
using JsonSerializationException = Newtonsoft.Json.JsonSerializationException;

namespace Liquidwarp.Infil.Tests;

public sealed class ServerLocationsTests
{
    [Test]
    public void ExtractUsesTheServerKeyAndDeserializesOnlyTheSelectedLocation()
    {
        const string json = """
            {
              "locations": { "requested": { "Id": "outside-data" } },
              "data": {
                "locations": {
                  "other": {
                    "Id": "requested", "SpawnPointParams": "invalid in an unrelated map",
                    "nested": [{ "requested": { "Id": "nested" } }]
                  },
                  "requested": {
                    "_Id": "backend-id", "Id": "map-variant",
                    "SpawnPointParams": [{
                      "Id": "gate", "Position": { "x": -12.5, "y": 2, "z": 45 },
                      "Categories": ["Player", "Coop"], "Sides": ["Usec", "Savage"],
                      "ColliderParams": { "_parent": "SpawnSphereParams", "_props": { "Radius": 20 } }
                    }]
                  }
                }
              }
            }
            """;

        var location = ServerLocations.Extract(json, "requested");

        Assert.That(location.Id, Is.EqualTo("map-variant"));
        Assert.That(location._Id, Is.EqualTo("backend-id"));
        var point = location.SpawnPointParams.Single();
        Assert.That(point.Id, Is.EqualTo("gate"));
        Assert.That((point.Position.x, point.Position.y, point.Position.z), Is.EqualTo((-12.5f, 2f, 45f)));
        Assert.That(point.Categories, Is.EqualTo(ESpawnCategoryMask.Player | ESpawnCategoryMask.Coop));
        Assert.That(point.Sides, Is.EqualTo(EPlayerSideMask.Usec | EPlayerSideMask.Savage));
        Assert.That(point.ColliderParams, Is.TypeOf<SpawnSphereParams>());
    }

    [TestCase("""{"locations":{"wanted":{"Id":"outside-data"}}}""")]
    [TestCase("""{"data":{"subobject":{"locations":{"wanted":{"Id":"nested"}}}}}""")]
    [TestCase("""{"data":{"locations":{}}}""")]
    [TestCase("""{"data":{"locations":{"other":{"Id":"wanted"}}}}""")]
    public void AbsentLocationReturnsNull(string response)
    {
        Assert.That(ServerLocations.Extract(response, "wanted"), Is.Null);
    }

    [Test]
    public void InvalidSelectedSpawnDataPropagatesToThePickerFallback()
    {
        const string json = """{"data":{"locations":{"wanted":{"SpawnPointParams":"invalid"}}}}""";
        Assert.Throws<JsonSerializationException>(() => ServerLocations.Extract(json, "wanted"));
    }

    [Test]
    public void InstalledFactorySpawnPointsSurviveTheServerResponseAndPickerPipeline()
    {
        var path = Path.Combine(GameInstallation.Root, "SPT_Runtime", "SPT_Data", "database", "locations", "factory4_day", "base.json");
        if (!File.Exists(path))
            Assert.Ignore("The supplied client installation has no Factory server database.");

        var databaseJson = File.ReadAllText(path);
        using var database = JsonDocument.Parse(databaseJson);
        var row = database.RootElement;
        var key = row.GetProperty("_Id").GetString()!;
        var source = row.GetProperty("SpawnPointParams");
        var response = JsonSerializer.Serialize(new { data = new { locations = new Dictionary<string, JsonElement> { [key] = row } } });

        var location = ServerLocations.Extract(response, key);
        var spots = SpawnSpots.For(location, EPlayerSide.Usec, null);

        Assert.That(location.Id, Is.EqualTo(row.GetProperty("Id").GetString()));
        Assert.That(location.SpawnPointParams.Select(point => point.Id),
            Is.EqualTo(source.EnumerateArray().Select(point => point.GetProperty("Id").GetString())));
        var expectedUsecIds = source.EnumerateArray().Where(point =>
            point.GetProperty("Categories").EnumerateArray().Any(value => value.GetString() is "Player" or "All") &&
            point.GetProperty("Sides").EnumerateArray().Any(value => value.GetString() is "Usec" or "Pmc" or "All"))
            .Select(point => point.GetProperty("Id").GetString()).ToArray();
        Assert.That(expectedUsecIds, Is.Not.Empty);
        Assert.That(spots.SelectMany(spot => spot.PointIds), Is.EquivalentTo(expectedUsecIds));
    }
}
