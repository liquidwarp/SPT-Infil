using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DynamicMaps.Data;
using EFT;
using EFT.UI.Matchmaker;
using HarmonyLib;
using JsonType;
using Liquidwarp.Infil.Compat;
using Liquidwarp.Infil.Map;
using Liquidwarp.Infil.Spawn;
using Liquidwarp.Infil.UI;
using SPT.Reflection.Patching;
using Controller = EFT.UI.Matchmaker.MatchMakerSelectionLocationScreen.SelectionLocationScreenController;

namespace Liquidwarp.Infil.Patches;

public class NextScreenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Controller), nameof(Controller.ShowNextScreen));

    [PatchPrefix]
    [HarmonyPriority(Priority.Low)]
    public static bool Prefix(object __instance, bool __runOriginal) =>
        LocationScreenAdvance.Intercept(__instance, __runOriginal, ready: false);
}

public class ReadyScreenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Controller), nameof(Controller.ShowReadyScreen));

    [PatchPrefix]
    [HarmonyPriority(Priority.Low)]
    public static bool Prefix(object __instance, bool __runOriginal) =>
        LocationScreenAdvance.Intercept(__instance, __runOriginal, ready: true);
}

public static class LocationScreenAdvance
{
    private static readonly AccessTools.FieldRef<MatchMakerSelectionLocationScreen, Profile> Profile =
        AccessTools.FieldRefAccess<MatchMakerSelectionLocationScreen, Profile>("_profile");

    private static readonly TimeSpan ServerTimeout = TimeSpan.FromSeconds(10);

    private static bool _passThrough;
    private static bool _fetching;

    public static bool Intercept(object instance, bool runOriginal, bool ready)
    {
        if (instance is not Controller controller)
            return true;

        if (_passThrough)
        {
            _passThrough = false;
            return true;
        }

        if (!runOriginal)
            return true;

        if (_fetching)
            return false;

        try
        {
            var location = controller.Screen.SelectedLocation;
            if (location == null || controller.RaidSettings.isInTransition)
                return true;

            if (ManimalLabs.Replaces(location.Id))
                return !OpenPicker(controller, location, DynamicMapsExtendedMaps.FindBackport(location.Id) ?? MapDefCatalog.Find(location.Id), () => Advance(controller, ready, false), $"No DynamicMaps map for {location.Id}");

            if (MapVariantsMaps.Manages(location.Id))
            {
                var backport = MapVariantsMaps.ChoseBackport(location.Id);
                if (backport == null)
                {
                    LogDefaultSpawns($"MapVariants recorded no choice for {location.Id}");
                    return true;
                }

                _fetching = true;
                OpenForVariant(controller, location.Id, backport.Value, () => Advance(controller, ready, true));
                return false;
            }

            return !OpenPicker(controller, location, MapDefCatalog.Find(location.Id), () => Advance(controller, ready, false), $"No DynamicMaps map for {location.Id}");
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
            return true;
        }
    }

    private static async void OpenForVariant(Controller controller, string locationId, bool backport, Action advance)
    {
        var opened = false;
        try
        {
            var variant = backport ? "backport" : "original";
            var mapDef = backport ? DynamicMapsExtendedMaps.FindBackport(locationId) : MapDefCatalog.Find(locationId);
            if (mapDef == null)
            {
                LogDefaultSpawns($"No DynamicMaps map for the {variant} {locationId}");
            }
            else
            {
                var served = MapVariantsMaps.ServesBackport(locationId);
                var fetched = ServerLocations.Fetch(locationId);
                var servesBackport = await WithTimeout(served);
                if (!controller.Active)
                    return;

                if (servesBackport != backport)
                {
                    LogDefaultSpawns($"The server is not serving the {variant} {locationId}");
                }
                else
                {
                    var location = await WithTimeout(fetched);
                    if (!controller.Active)
                        return;

                    if (location == null)
                        LogDefaultSpawns($"The server sent no data for {locationId}");
                    else
                        opened = ShowPicker(controller, location, mapDef, advance);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
        }
        finally
        {
            _fetching = false;
        }

        if (!opened && controller.Active)
            advance();
    }

    private static bool OpenPicker(Controller controller, LocationSettings.Location location, MapDef mapDef, Action advance, string missing)
    {
        if (mapDef != null)
            return ShowPicker(controller, location, mapDef, advance);

        LogDefaultSpawns(missing);
        return false;
    }

    private static bool ShowPicker(Controller controller, LocationSettings.Location location, MapDef mapDef, Action advance)
    {
        var screen = controller.Screen;
        var side = controller.RaidSettings.Side;
        var playerSide = side == ESideType.Savage ? EPlayerSide.Savage : Profile(screen).Info.Side;
        return SpawnPickerWindow.Open(mapDef, location, side, playerSide, screen, screen._acceptButton, advance);
    }

    private static void Advance(Controller controller, bool ready, bool skipPatches)
    {
        if (skipPatches)
        {
            (ready ? controller._onShowReadyScreen : controller._onShowNextScreen)?.Invoke();
            return;
        }

        _passThrough = true;
        if (ready)
            controller.ShowReadyScreen();
        else
            controller.ShowNextScreen();
    }

    private static async Task<T> WithTimeout<T>(Task<T> task)
    {
        using var timeout = new CancellationTokenSource();
        if (await Task.WhenAny(task, Task.Delay(ServerTimeout, timeout.Token)) == task)
        {
            timeout.Cancel();
            return await task;
        }

        Plugin.LogSource.LogWarning($"The server took over {ServerTimeout.TotalSeconds:0} seconds to answer");
        return default;
    }

    private static void LogDefaultSpawns(string reason) =>
        Plugin.LogSource.LogInfo($"{reason}, using default spawns");
}
