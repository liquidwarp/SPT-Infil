using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EFT;
using Liquidwarp.Infil.Patches;
using Liquidwarp.Infil.Spawn;
using NUnit.Framework;

namespace Liquidwarp.Infil.Tests;

[NonParallelizable]
public sealed class SpawnChoiceTests
{
    [SetUp]
    public void SetUp() => SpawnChoiceStore.Clear();

    [TearDown]
    public void TearDown() => SpawnChoiceStore.Clear();

    [TestCase("Woods", "woODS")]
    [TestCase("Sandbox_high", "SANDBOX")]
    [TestCase("sandbox", "Sandbox_HIGH")]
    public void SelectionSurvivesEquivalentLocationIdsButOnlyForItsOwnSide(string selected, string actual)
    {
        var spot = new SpawnSpot { Label = "#2" };
        SpawnChoiceStore.Set(selected, ESideType.Savage, spot);

        Assert.That(SpawnChoiceStore.Get(actual, ESideType.Savage), Is.SameAs(spot));
        Assert.That(SpawnChoiceStore.Get(actual, ESideType.Pmc), Is.Null);
        Assert.That(SpawnChoiceStore.Get(null, ESideType.Savage), Is.Null);
        Assert.That(SpawnChoiceStore.Get("factory4_day", ESideType.Savage), Is.Null);
    }

    [Test]
    public void ANewSelectionReplacesTheOldOneAndCanBeClearedByChoosingRandom()
    {
        var first = new SpawnSpot { Label = "#1" };
        SpawnChoiceStore.Set("Woods", ESideType.Pmc, first);
        SpawnChoiceStore.MarkReached();
        Assert.That(SpawnChoiceStore.Get("Woods", ESideType.Pmc), Is.SameAs(first));
        Assert.That(SpawnChoiceStore.Unreached, Is.Null);

        var second = new SpawnSpot { Label = "#3" };
        SpawnChoiceStore.Set("Lighthouse", ESideType.Savage, second);
        Assert.That(SpawnChoiceStore.Get("Woods", ESideType.Pmc), Is.Null);
        Assert.That(SpawnChoiceStore.Get("Lighthouse", ESideType.Savage), Is.SameAs(second));
        Assert.That(SpawnChoiceStore.Unreached, Does.Contain(second.Title));

        SpawnChoiceStore.Set("Lighthouse", ESideType.Savage, null);
        Assert.That(SpawnChoiceStore.Get("Lighthouse", ESideType.Savage), Is.Null);
        Assert.That(SpawnChoiceStore.Unreached, Is.Null);
    }

    [TestCase("Sandbox", false, LogLevel.Warning)]
    [TestCase("Woods", false, LogLevel.Info)]
    [TestCase("Sandbox_high", true, LogLevel.None)]
    public void RaidStartClearsTheChoiceAndReportsAnUnappliedChoiceOnlyOnce(string raidMap, bool reached, LogLevel expected)
    {
        // Only the location ID is needed from the Unity world at this stage.
        var world = (GameWorld)RuntimeHelpers.GetUninitializedObject(typeof(ClientLocalGameWorld));
        world.LocationId = raidMap;
        var spot = new SpawnSpot { Label = "#4" };
        SpawnChoiceStore.Set("Sandbox_high", ESideType.Pmc, spot);
        if (reached)
            SpawnChoiceStore.MarkReached();

        var messages = new List<LogEventArgs>();
        using var log = new ManualLogSource(nameof(SpawnChoiceTests));
        log.LogEvent += (_, message) => messages.Add(message);
        var previousLog = Plugin.LogSource;
        try
        {
            Plugin.LogSource = log;
            RaidStartedPatch.Postfix(world);
            RaidStartedPatch.Postfix(world);
        }
        finally
        {
            Plugin.LogSource = previousLog;
        }

        Assert.That(SpawnChoiceStore.Get("Sandbox_high", ESideType.Pmc), Is.Null);
        Assert.That(SpawnChoiceStore.Unreached, Is.Null);
        if (expected == LogLevel.None)
            Assert.That(messages, Is.Empty);
        else
        {
            var message = messages.Single();
            Assert.That(message.Level, Is.EqualTo(expected));
            Assert.That(message.Data.ToString(), Does.Contain(spot.Title));
        }
    }
}
