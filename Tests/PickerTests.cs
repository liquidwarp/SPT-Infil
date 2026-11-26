using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using DynamicMaps.Data;
using DynamicMaps.UI.Components;
using EFT;
using EFT.Game.Spawning;
using EFT.UI.Matchmaker;
using JsonType;
using Liquidwarp.Infil.Patches;
using Liquidwarp.Infil.Spawn;
using Liquidwarp.Infil.UI;
using NUnit.Framework;
using UnityEngine;
using Controller = EFT.UI.Matchmaker.MatchMakerSelectionLocationScreen.SelectionLocationScreenController;

namespace Liquidwarp.Infil.Tests;

[NonParallelizable]
public sealed class PickerTests
{
    private ManualLogSource previousLogger;

    [SetUp]
    public void SetUp()
    {
        previousLogger = Plugin.LogSource;
        Plugin.LogSource = new ManualLogSource(nameof(PickerTests));
        SpawnChoiceStore.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        SpawnChoiceStore.Clear();
        Plugin.LogSource.Dispose();
        Plugin.LogSource = previousLogger;
    }

    [TestCase(nameof(Controller.ShowNextScreen))]
    [TestCase(nameof(Controller.ShowReadyScreen))]
    public void TransitClearsTheChoiceAndContinuesThroughTheRequestedButtonOnce(string button)
    {
        var spot = new SpawnSpot { Label = "#3" };
        SpawnChoiceStore.Set("Woods", ESideType.Pmc, spot);
        var controller = LocationController(new LocationSettings.Location { Id = "Woods" }, transit: true);
        var calls = new List<string>();
        controller._onShowNextScreen = () => calls.Add(nameof(Controller.ShowNextScreen));
        controller._onShowReadyScreen = () => calls.Add(nameof(Controller.ShowReadyScreen));

        var runOriginal = AdvanceScreenPatch.Prefix(controller, true, typeof(Controller).GetMethod(button)!);

        Assert.That(runOriginal, Is.False);
        Assert.That(calls, Is.EqualTo(new[] { button }));
        Assert.That(SpawnChoiceStore.Get("Woods", ESideType.Pmc), Is.Null);
    }

    [Test]
    public void OtherControllersSkippedAdvancesAndEmptyLocationsPreserveThePendingChoice()
    {
        var spot = new SpawnSpot { Label = "#1" };
        SpawnChoiceStore.Set("Woods", ESideType.Pmc, spot);
        var controller = LocationController(null);
        var advanced = false;
        controller._onShowReadyScreen = () => advanced = true;

        Assert.That(LocationScreenAdvance.Intercept(new object(), true, true), Is.True);
        Assert.That(LocationScreenAdvance.Intercept(controller, true, true), Is.True);
        controller = LocationController(new LocationSettings.Location { Id = "Woods" }, transit: true);
        controller._onShowReadyScreen = () => advanced = true;
        Assert.That(LocationScreenAdvance.Intercept(controller, false, true), Is.True);

        Assert.That(advanced, Is.False);
        Assert.That(SpawnChoiceStore.Get("Woods", ESideType.Pmc), Is.SameAs(spot));
    }

    [Test]
    public void AClosedLocationScreenClearsTheStaleChoiceAndLetsTheGameAdvance()
    {
        SpawnChoiceStore.Set("Woods", ESideType.Pmc, new SpawnSpot { Label = "#1" });
        var controller = (Controller)RuntimeHelpers.GetUninitializedObject(typeof(Controller));

        Assert.That(LocationScreenAdvance.Intercept(controller, true, false), Is.True);
        Assert.That(SpawnChoiceStore.Get("Woods", ESideType.Pmc), Is.Null);
    }

    [Test]
    public void AMapWithOnlyPmcSpawnsFallsBackForAScavRaid()
    {
        var location = new LocationSettings.Location
        {
            Id = "Woods",
            SpawnPointParams = [new() { Id = "pmc", Sides = EPlayerSideMask.Pmc, Categories = ESpawnCategoryMask.Player, Position = new() }]
        };
        var controller = LocationController(location);
        controller.RaidSettings.Side = ESideType.Savage;

        var reason = LocationScreenAdvance.OpenPicker(controller, location, new MapDef(), () => Assert.Fail("Unexpected advance"));

        Assert.That(reason, Is.EqualTo("No Savage spawn point on Woods"));
    }

    [TestCase("Sandbox_high", ESideType.Pmc, "near Gate", "Spawn area #3, near Gate")]
    [TestCase("Sandbox", ESideType.Pmc, null, "Spawn area #3")]
    [TestCase("Sandbox", ESideType.Savage, null, null)]
    [TestCase("Woods", ESideType.Pmc, null, null)]
    [TestCase(null, ESideType.Pmc, null, null)]
    public void ConfirmationDescribesOnlyTheChoiceForThisRaid(string map, ESideType side, string where, string expected)
    {
        SpawnChoiceStore.Set("Sandbox", ESideType.Pmc, new SpawnSpot { Label = "#3", Where = where });
        var raid = new RaidSettings { Side = side, SelectedLocation = map == null ? null : new LocationSettings.Location { Id = map } };

        Assert.That(ConfirmationScreenPatch.Describe(raid), Is.EqualTo(expected));
    }

    [Test]
    public void FloorChoiceUsesTheSmallestContainingBoundInEitherLayerOrder()
    {
        var building = LayerWithBounds(10);
        var room = LayerWithBounds(5);

        Assert.That(SpawnPickerWindow.FindLayer([building, room], Vector3.zero), Is.SameAs(room));
        Assert.That(SpawnPickerWindow.FindLayer([room, building], Vector3.zero), Is.SameAs(room));
        Assert.That(SpawnPickerWindow.FindLayer([room, building], new Vector3(0, 8, 0)), Is.SameAs(building));
        Assert.That(SpawnPickerWindow.FindLayer([room, building], new Vector3(0, 0, 12)), Is.Null);
    }

    [Test]
    public void FloorSummaryFallsBackToTheTotalOrCountsUnassignedPoints()
    {
        var spot = new SpawnSpot { PointPositions = [new Vector3(20, 4, 0), new Vector3(0, 0, 40)] };

        Assert.That(SpawnPickerWindow.DescribeLevels([], spot), Is.Null);
        Assert.That(SpawnPickerWindow.DescribeLevels([LayerWithBounds(5)], spot), Is.EqualTo("Other: 2"));
    }

    private static Controller LocationController(LocationSettings.Location location, bool transit = false)
    {
        // A live screen constructor needs the Unity player. These paths only read its selected location.
        var controller = (Controller)RuntimeHelpers.GetUninitializedObject(typeof(Controller));
        controller.Screen = (MatchMakerSelectionLocationScreen)RuntimeHelpers.GetUninitializedObject(typeof(MatchMakerSelectionLocationScreen));
        typeof(MatchMakerSelectionLocationScreen).GetField("_selectedLocation", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(controller.Screen, location);
        typeof(Controller).GetField(nameof(Controller.RaidSettings))!.SetValue(controller, new RaidSettings { isInTransition = transit });
        return controller;
    }

    private static MapLayer LayerWithBounds(float halfSize)
    {
        var layer = (MapLayer)RuntimeHelpers.GetUninitializedObject(typeof(MapLayer));
        var bounds = new BoundingRectangularSolid { Min = Vector3.one * -halfSize, Max = Vector3.one * halfSize };
        typeof(MapLayer).GetField("_def", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(layer, new MapLayerDef { GameBounds = [bounds] });
        return layer;
    }
}
