using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Comfort.Common;
using EFT;
using JsonType;
using Liquidwarp.Infil.Compat;
using Liquidwarp.Infil.Spawn;
using NUnit.Framework;

namespace Liquidwarp.Infil.Tests;

[TestFixture, NonParallelizable]
public class FikaSpawnTests
{
    private FieldInfo _getterField;
    private MethodInfo _previousGetter;
    private MethodInfo _spawnTogether;
    private PropertyInfo _settings;
    private GameWorld _previousWorld;

    [OneTimeSetUp]
    public void BindInstalledFikaController()
    {
        var path = Path.Combine(GameInstallation.Root, "BepInEx", "plugins", "Fika", "Fika.Core.dll");
        if (!File.Exists(path))
            Assert.Ignore("Fika is optional. Select an SPT install with Fika to run these co-op tests.");

        var controller = Assembly.LoadFrom(path).GetType("Fika.Core.Main.GameMode.ClientGameController", throwOnError: true);
        _spawnTogether = controller.GetMethod("CheckSpawnTogether", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, []);
        _settings = controller.GetProperty("RaidSettings");
        Assert.That(_spawnTogether?.ReturnType, Is.EqualTo(typeof(bool)));
        Assert.That(_settings?.PropertyType, Is.EqualTo(typeof(RaidSettings)));

        // BepInEx normally binds this getter while registering the Harmony patch in Unity.
        _getterField = typeof(FikaClientSpawnPatch).GetField("_getRaidSettings", BindingFlags.NonPublic | BindingFlags.Static);
        _previousGetter = (MethodInfo)_getterField.GetValue(null);
        _getterField.SetValue(null, _settings.GetMethod);
    }

    [OneTimeTearDown]
    public void RestoreBinding() => _getterField?.SetValue(null, _previousGetter);

    [SetUp]
    public void EnterRaid()
    {
        _previousWorld = Singleton<GameWorld>.Instance;
        var world = (GameWorld)RuntimeHelpers.GetUninitializedObject(typeof(ClientLocalGameWorld));
        world.LocationId = "woods";
        Singleton<GameWorld>.Instance = world;
        SpawnChoiceStore.Clear();
    }

    [TearDown]
    public void LeaveRaid()
    {
        SpawnChoiceStore.Clear();
        Singleton<GameWorld>.Instance = _previousWorld;
    }

    [TestCase(true, null, ESideType.Pmc, ESideType.Pmc, true)]
    [TestCase(true, "Woods", ESideType.Pmc, ESideType.Pmc, false)]
    [TestCase(true, "Woods", ESideType.Savage, ESideType.Savage, false)]
    [TestCase(true, "Woods", ESideType.Savage, ESideType.Pmc, true)]
    [TestCase(true, "factory4_day", ESideType.Pmc, ESideType.Pmc, true)]
    [TestCase(false, null, ESideType.Pmc, ESideType.Pmc, false)]
    [TestCase(false, "Woods", ESideType.Pmc, ESideType.Pmc, false)]
    public void PersonalChoiceOverridesOnlyAMatchingSpawnTogetherRaid(bool together, string chosenMap,
        ESideType chosenSide, ESideType side, bool expected)
    {
        if (chosenMap != null)
            SpawnChoiceStore.Set(chosenMap, chosenSide, new SpawnSpot { Label = "#2" });

        var controller = RuntimeHelpers.GetUninitializedObject(_spawnTogether.DeclaringType);
        _settings.SetValue(controller, new RaidSettings
        {
            Side = side,
            SelectedLocation = new LocationSettings.Location { Id = "Woods" },
            PlayersSpawnPlace = together ? EPlayersSpawnPlace.SamePlace : EPlayersSpawnPlace.DifferentPlaces,
        });
        var result = (bool)_spawnTogether.Invoke(controller, null);
        Assert.That(result, Is.EqualTo(together), "Fika's group setting before Infil runs");

        FikaClientSpawnPatch.Postfix(controller, ref result);

        Assert.That(result, Is.EqualTo(expected));
    }
}
