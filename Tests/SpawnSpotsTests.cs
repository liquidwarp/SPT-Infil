using System.Linq;
using DynamicMaps.Data;
using EFT;
using EFT.Game.Spawning;
using JsonType;
using Liquidwarp.Infil.Spawn;
using NUnit.Framework;
using UnityEngine;

namespace Liquidwarp.Infil.Tests;

[TestFixture]
public class SpawnSpotsTests
{
    [TestCase(EPlayerSide.Usec, new[] { "usec", "pmcs", "everyone" })]
    [TestCase(EPlayerSide.Bear, new[] { "bear", "pmcs", "everyone" })]
    [TestCase(EPlayerSide.Savage, new[] { "scav", "everyone" })]
    public void OnlyPlayerPointsWithAPositionAndMatchingSideAreOffered(EPlayerSide side, string[] expected)
    {
        var missingPosition = Point("no position");
        missingPosition.Position = null;
        var location = Location("woods",
            Point("usec", sides: EPlayerSideMask.Usec),
            Point("bear", sides: EPlayerSideMask.Bear),
            Point("scav", sides: EPlayerSideMask.Savage),
            Point("pmcs", sides: EPlayerSideMask.Pmc),
            Point("everyone", categories: ESpawnCategoryMask.Player | ESpawnCategoryMask.Bot),
            Point("bot", categories: ESpawnCategoryMask.Bot),
            Point("boss", categories: ESpawnCategoryMask.Boss),
            Point("no category", categories: ESpawnCategoryMask.None),
            Point("no sides", sides: EPlayerSideMask.None),
            missingPosition);

        var area = SpawnSpots.For(location, side, null).Single();

        Assert.That(area.PointIds, Is.EquivalentTo(expected));
        Assert.That(area.PointPositions, Has.Count.EqualTo(expected.Length));
    }

    [Test]
    public void LocationsWithoutPlayerPointsOfferNoAreas()
    {
        var onlyBots = Location("woods", Point("bot", categories: ESpawnCategoryMask.Bot));
        foreach (var location in new[] { Location("woods", null), Location("woods"), onlyBots })
            Assert.That(SpawnSpots.For(location, EPlayerSide.Usec, null), Is.Empty);
    }

    [TestCase("FACTORY4_DAY", 50)]
    [TestCase("factory4_night", 50)]
    [TestCase("laboratory", 30)]
    [TestCase("labyrinth", 25)]
    [TestCase("RezervBase", 125)]
    [TestCase("Lighthouse", 125)]
    [TestCase("Sandbox", 100)]
    [TestCase("Sandbox_high", 100)]
    [TestCase("woods", 200)]
    public void SpreadLimitIsInclusiveAndDependsOnTheLocation(string map, float limit)
    {
        var location = Location(map,
            Point("west"), Point("east", x: limit),
            Point("south", x: 1000f), Point("north", x: 1000f, z: limit + 1f));

        var areas = SpawnSpots.For(location, EPlayerSide.Usec, null);

        Assert.That(areas, Has.Count.EqualTo(3));
        Assert.That(areas[0].PointIds, Is.EquivalentTo(new[] { "west", "east" }));
        Assert.That(areas[1].PointIds, Is.EquivalentTo(new[] { "south" }));
        Assert.That(areas[2].PointIds, Is.EquivalentTo(new[] { "north" }));
    }

    [Test]
    public void GroupsTheClosestPointsWithoutJoiningAChainWiderThanTheArea()
    {
        // Each consecutive pair is within Factory's 50 m limit. The three inner points are closest.
        var location = Location("factory4_day", Point("a"), Point("b", 28f), Point("c", 33f),
            Point("d", 55f), Point("e", 91f));

        var areas = SpawnSpots.For(location, EPlayerSide.Usec, null);

        Assert.That(areas, Has.Count.EqualTo(3));
        Assert.That(areas[0].PointIds, Is.EquivalentTo(new[] { "a" }));
        Assert.That(areas[1].PointIds, Is.EquivalentTo(new[] { "b", "c", "d" }));
        Assert.That(areas[2].PointIds, Is.EquivalentTo(new[] { "e" }));
        Assert.That(areas[1].Position.x, Is.EqualTo(116f / 3f).Within(0.0001f));
        Assert.That(areas.Select(area => area.Title), Is.EqualTo(new[] { "Spawn area #1", "Spawn area #2", "Spawn area #3" }));
        Assert.That(areas[0].Where, Is.Null);
    }

    [TestCase(30, 40, 1)]
    [TestCase(36, 36, 2)]
    public void DiagonalSpreadUsesDistanceInTheWorldPlane(float x, float z, int expectedAreas)
    {
        var location = Location("factory4_day", Point("origin"), Point("diagonal", x: x, z: z));

        Assert.That(SpawnSpots.For(location, EPlayerSide.Usec, null), Has.Count.EqualTo(expectedAreas));
    }

    [Test]
    public void StackedFloorsShareAnAreaAndRetainTheirWorldPositions()
    {
        var map = new MapDef
        {
            Labels =
            [
                new MapLabelDef { Text = "Courtyard", Position = new Vector3(15, 20, 0) },
                new MapLabelDef { Text = "Office", Position = new Vector3(15, 100, 0) }
            ]
        };
        var lower = new Vector3(0, -200, 0);
        var upper = new Vector3(30, 400, 40);
        var location = Location("factory4_night", Point("lower", lower.x, lower.y, lower.z),
            Point("upper", upper.x, upper.y, upper.z));

        var area = SpawnSpots.For(location, EPlayerSide.Bear, map).Single();

        // The horizontal separation is exactly 50 m. World height contributes to the center only.
        Assert.That(area.PointPositions, Is.EquivalentTo(new[] { lower, upper }));
        Assert.That(area.Position, Is.EqualTo(new Vector3(15, 100, 20)));
        Assert.That(area.Where, Is.EqualTo("near Courtyard"));
    }

    private static LocationSettings.Location Location(string id, params SpawnPointParams[] points) =>
        new() { Id = id, SpawnPointParams = points };

    private static SpawnPointParams Point(string id, float x = 0f, float y = 0f, float z = 0f,
        EPlayerSideMask sides = EPlayerSideMask.All, ESpawnCategoryMask categories = ESpawnCategoryMask.Player) =>
        new() { Id = id, Position = new Vector3(x, y, z), Sides = sides, Categories = categories };
}
