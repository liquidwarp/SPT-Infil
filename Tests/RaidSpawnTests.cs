using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using Liquidwarp.Infil.Patches;
using Liquidwarp.Infil.Spawn;
using NUnit.Framework;
using UnityEngine;

namespace Liquidwarp.Infil.Tests;

[TestFixture, NonParallelizable]
public class RaidSpawnTests
{
    private const string PmcId = "000000000000000000000001";
    private const string ScavId = "000000000000000000000002";
    private const string Map = "woods";

    private GameWorld _world;
    private GameWorld _previousWorld;
    private ManualLogSource _previousLog;
    private EftClientBackendSession _session;

    [SetUp]
    public void SetUp()
    {
        _previousWorld = Singleton<GameWorld>.Instance;
        _previousLog = Plugin.LogSource;
        Plugin.LogSource = new ManualLogSource(nameof(RaidSpawnTests));
        SpawnChoiceStore.Clear();

        // Bypass the Unity scene and backend constructors. Selection only reads the map,
        // registered players and the session's two profiles.
        _world = (GameWorld)RuntimeHelpers.GetUninitializedObject(typeof(ClientLocalGameWorld));
        _world.LocationId = Map;
        typeof(GameWorld).GetField(nameof(GameWorld.RegisteredPlayers)).SetValue(_world, new List<IPlayer>());
        Singleton<GameWorld>.Instance = _world;

        var pmc = (Profile)RuntimeHelpers.GetUninitializedObject(typeof(Profile));
        var scav = (Profile)RuntimeHelpers.GetUninitializedObject(typeof(Profile));
        pmc.Id = PmcId;
        scav.Id = ScavId;
        typeof(Profile).GetField(nameof(Profile.PetId)).SetValue(pmc, new MongoID(ScavId));
        _session = (EftClientBackendSession)RuntimeHelpers.GetUninitializedObject(typeof(EftClientBackendSession));
        _session._profile = pmc;
        _session._allProfiles = [pmc, scav];
    }

    [TearDown]
    public void TearDown()
    {
        SpawnChoiceStore.Clear();
        Singleton<GameWorld>.Instance = _previousWorld;
        Plugin.LogSource = _previousLog;
    }

    [TestCase(ESpawnCategory.Bot)]
    [TestCase(ESpawnCategory.Boss)]
    [TestCase(ESpawnCategory.Coop)]
    [TestCase(ESpawnCategory.Group)]
    public void NonPlayerSpawnNeverConsumesTheLocalChoice(ESpawnCategory category)
    {
        var vanilla = Point("vanilla");
        ISpawnPoint result = vanilla;
        Choose(ESideType.Pmc, "chosen");
        var logged = new List<LogEventArgs>();
        Plugin.LogSource.LogEvent += (_, message) => logged.Add(message);

        SelectSpawnPointPatch.Postfix(null, category, EPlayerSide.Usec, null, PmcId, ref result);

        Assert.That(result, Is.SameAs(vanilla));
        Assert.That(SpawnChoiceStore.Unreached, Is.Not.Null);
        Assert.That(logged, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("another-player")]
    public void ForeignOrMissingProfileKeepsVanillaAndLeavesChoicePending(string profileId)
    {
        var vanilla = Point("vanilla");
        ISpawnPoint result = vanilla;
        Choose(ESideType.Pmc, "chosen");

        SelectSpawnPointPatch.Apply(SystemWith(Point("chosen")), EPlayerSide.Usec, null, profileId, _session, ref result);

        Assert.That(result, Is.SameAs(vanilla));
        Assert.That(SpawnChoiceStore.Unreached, Is.Not.Null);
    }

    [Test]
    public void MissingSessionLeavesChoicePending()
    {
        var vanilla = Point("vanilla");
        ISpawnPoint result = vanilla;
        Choose(ESideType.Pmc, "chosen");

        SelectSpawnPointPatch.Apply(SystemWith(Point("chosen")), EPlayerSide.Usec, null, PmcId, null, ref result);

        Assert.That(result, Is.SameAs(vanilla));
        Assert.That(SpawnChoiceStore.Unreached, Is.Not.Null);
    }

    [TestCase("factory4_day", ESideType.Pmc)]
    [TestCase(Map, ESideType.Savage)]
    public void ChoiceForAnotherMapOrSideLeavesVanillaPoint(string map, ESideType side)
    {
        var vanilla = Point("vanilla");
        ISpawnPoint result = vanilla;
        SpawnChoiceStore.Set(map, side, new SpawnSpot { Label = "#1", PointIds = ["chosen"] });

        SelectSpawnPointPatch.Apply(SystemWith(Point("chosen")), EPlayerSide.Bear, null, PmcId, _session, ref result);

        Assert.That(result, Is.SameAs(vanilla));
        Assert.That(SpawnChoiceStore.Unreached, Is.Not.Null);
    }

    [Test]
    public void RandomChoiceLeavesVanillaPoint()
    {
        var vanilla = Point("vanilla");
        ISpawnPoint result = vanilla;

        SelectSpawnPointPatch.Apply(SystemWith(Point("other")), EPlayerSide.Usec, null, PmcId, _session, ref result);

        Assert.That(result, Is.SameAs(vanilla));
    }

    [TestCase(EPlayerSide.Usec, ESideType.Pmc, PmcId)]
    [TestCase(EPlayerSide.Bear, ESideType.Pmc, PmcId)]
    [TestCase(EPlayerSide.Savage, ESideType.Savage, ScavId)]
    public void LocalPmcOrScavSpawnsAtTheChosenPoint(EPlayerSide faction, ESideType side, string profileId)
    {
        var vanilla = Point("vanilla");
        var chosen = Point("chosen");
        ISpawnPoint result = vanilla;
        Choose(side, chosen.Id);

        SelectSpawnPointPatch.Apply(SystemWith(vanilla, chosen), faction, null, profileId, _session, ref result);

        Assert.That(result, Is.SameAs(chosen));
        Assert.That(SpawnChoiceStore.Unreached, Is.Null);
    }

    [Test]
    public void VanillaPointInsideAreaIsKeptEvenWhenAnotherIsFartherFromPlayers()
    {
        var near = Point("near");
        var far = Point("far", 500);
        ISpawnPoint result = near;
        _world.RegisteredPlayers.Add(PlayerAt(0));
        Choose(ESideType.Pmc, near.Id, far.Id);

        SelectSpawnPointPatch.Apply(SystemWith(near, far), EPlayerSide.Usec, null, PmcId, _session, ref result);

        Assert.That(result, Is.SameAs(near));
        Assert.That(SpawnChoiceStore.Unreached, Is.Null);
    }

    [Test]
    public void MissingAreaKeepsVanillaPointAndMarksTheAttempt()
    {
        var vanilla = Point("vanilla");
        ISpawnPoint result = vanilla;
        Choose(ESideType.Pmc, "stale-id");
        var logged = new List<LogLevel>();
        Plugin.LogSource.LogEvent += (_, message) => logged.Add(message.Level);

        SelectSpawnPointPatch.Apply(SystemWith(vanilla), EPlayerSide.Usec, null, PmcId, _session, ref result);

        Assert.That(result, Is.SameAs(vanilla));
        Assert.That(SpawnChoiceStore.Unreached, Is.Null);
        Assert.That(logged, Does.Contain(LogLevel.Warning));
    }

    [Test]
    public void AServerAreaStillPlacesThePlayerWhenOnlyOneOfItsPointsRemainsInTheScene()
    {
        const string response = """
            {"data":{"locations":{"map-key":{"Id":"woods","SpawnPointParams":[
              {"Id":"gate","Position":{"x":0,"y":0,"z":0},"Categories":["Player"],"Sides":["Pmc"]},
              {"Id":"shed","Position":{"x":15,"y":0,"z":0},"Categories":["Player"],"Sides":["Pmc"]},
              {"Id":"hill","Position":{"x":800,"y":0,"z":0},"Categories":["Player"],"Sides":["Pmc"]}
            ]}}}}
            """;
        var location = ServerLocations.Extract(response, "map-key");
        var areas = SpawnSpots.For(location, EPlayerSide.Bear, null);
        var selected = areas.Single(area => area.Label == "#1");
        Assert.That(selected.PointIds, Is.EquivalentTo(new[] { "gate", "shed" }));
        SpawnChoiceStore.Set(location.Id, ESideType.Pmc, selected);

        var shed = Point("shed", 15);
        ISpawnPoint result = Point("hill", 800);
        var scene = SystemWith(result, shed, Point("new-point", 5));
        SelectSpawnPointPatch.Apply(scene, EPlayerSide.Bear, null, PmcId, _session, ref result);

        Assert.That(result, Is.SameAs(shed));
        RaidStartedPatch.Postfix(_world);
        Assert.That(SpawnChoiceStore.Get(location.Id, ESideType.Pmc), Is.Null);
    }

    [TestCase(true, EPlayerSideMask.Usec)]
    [TestCase(false, EPlayerSideMask.Savage)]
    public void PrefersAUsablePointEvenIfAnIneligiblePointIsFarther(bool blocked, EPlayerSideMask sides)
    {
        var usable = Point("usable");
        var ineligible = Point("ineligible", 500);
        ineligible.SpawnBlocked = blocked;
        ineligible.Sides = sides;
        _world.RegisteredPlayers.Add(PlayerAt(0));
        Choose(ESideType.Pmc, usable.Id, ineligible.Id);
        ISpawnPoint result = Point("vanilla");

        SelectSpawnPointPatch.Apply(SystemWith(usable, ineligible), EPlayerSide.Usec, null, PmcId, _session, ref result);

        Assert.That(result, Is.SameAs(usable));
    }

    [Test]
    public void UsesTheChosenAreaAsFallbackWhenAllItsPointsAreBlocked()
    {
        var blocked = Point("blocked");
        blocked.SpawnBlocked = true;
        Choose(ESideType.Pmc, blocked.Id);
        ISpawnPoint result = Point("vanilla");

        SelectSpawnPointPatch.Apply(SystemWith(blocked), EPlayerSide.Usec, null, PmcId, _session, ref result);

        Assert.That(result, Is.SameAs(blocked));
    }

    [Test]
    public void AvoidsOtherPlayersAndExcludesThePlayerBeingSpawned()
    {
        var nearOther = Point("near-other");
        var nearSelf = Point("near-self", 500);
        var self = PlayerAt(500);
        _world.RegisteredPlayers.Add(PlayerAt(0));
        _world.RegisteredPlayers.Add(self);
        Choose(ESideType.Pmc, nearOther.Id, nearSelf.Id);
        ISpawnPoint result = Point("vanilla");

        SelectSpawnPointPatch.Apply(SystemWith(nearOther, nearSelf), EPlayerSide.Usec, self, PmcId, _session, ref result);

        Assert.That(result, Is.SameAs(nearSelf));
        Assert.That(((PositionedPlayer)(object)self).PositionReads, Is.Zero);
    }

    private static SpawnPoint Point(string id, float x = 0) =>
        new() { Id = id, Sides = EPlayerSideMask.All, Position = new Vector3(x, 0, 0) };

    private static void Choose(ESideType side, params string[] ids) =>
        SpawnChoiceStore.Set(Map, side, new SpawnSpot { Label = "#1", PointIds = new HashSet<string>(ids) });

    private SpawnSystem SystemWith(params ISpawnPoint[] points)
    {
        var collection = new SpawnPointsCollection(points.ToDictionary(point => point, _ => (SpawnPointMarker)null), null);
        return new SpawnSystem(SpawnSystemSettings.CreateDefault(), () => 0, _world, null, collection);
    }

    private static IPlayer PlayerAt(float x)
    {
        var player = DispatchProxy.Create<IPlayer, PositionedPlayer>();
        ((PositionedPlayer)(object)player).Location = new Vector3(x, 0, 0);
        return player;
    }

    // GameWorld enumerates living players, then EFT measures their distance from each point.
    public class PositionedPlayer : DispatchProxy
    {
        private readonly ObservedPlayerHealthController _health = new(null) { IsAlive = true };
        public Vector3 Location;
        public int PositionReads { get; private set; }

        protected override object Invoke(MethodInfo method, object[] args)
        {
            switch (method.Name)
            {
                case "get_HealthController":
                    return _health;
                case "get_Position":
                    PositionReads++;
                    return Location;
                default:
                    throw new NotSupportedException(method.Name);
            }
        }
    }
}
