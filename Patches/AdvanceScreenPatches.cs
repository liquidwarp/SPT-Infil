using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DynamicMaps.Data;
using EFT;
using EFT.UI;
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

    private static readonly Dictionary<(string, bool), LocationSettings.Location> VariantLocations = new();

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
            if (location == null)
                return true;

            if (controller.RaidSettings.isInTransition)
            {
                UseDefaultSpawns($"{location.Id} is reached by transit");
                return true;
            }

            // MapVariants' own prefix prompts again unless we skip it
            var skipPatches = MapVariantsMaps.Prompts(location.Id);
            Action advance = () => Advance(controller, ready, skipPatches);

            if (!MapVariantsMaps.Manages(location.Id))
                return !OpenPicker(controller, location, MapDefCatalog.Find(location.Id), advance);

            OpenForVariant(controller, location.Id, MapVariantsMaps.ChoseBackport(location.Id), advance);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
            SpawnChoiceStore.Clear();
            return true;
        }
    }

    private static async void OpenForVariant(Controller controller, string locationId, bool? chosen, Action advance)
    {
        _fetching = true;
        var buttons = LockButtons(controller.Screen);
        var opened = false;
        try
        {
            var served = await WithTimeout(MapVariantsMaps.ServesBackport(locationId));
            if (!StillShowing(controller, locationId))
                return;

            if (served == null)
            {
                UseDefaultSpawns($"The server did not say which {locationId} it serves");
            }
            else if (chosen != null && chosen != served)
            {
                UseDefaultSpawns($"The server is not serving the {Variant(chosen.Value)} {locationId}");
            }
            else
            {
                var backport = served.Value;
                var mapDef = backport ? DynamicMapsExtendedMaps.FindBackport(locationId) : MapDefCatalog.Find(locationId);
                if (mapDef == null)
                {
                    UseDefaultSpawns($"No DynamicMaps map for the {Variant(backport)} {locationId}");
                }
                else
                {
                    if (!VariantLocations.TryGetValue((locationId, backport), out var location))
                    {
                        location = await WithTimeout(ServerLocations.Fetch(locationId));
                        if (!StillShowing(controller, locationId))
                            return;

                        if (location != null)
                            VariantLocations[(locationId, backport)] = location;
                    }

                    if (location == null)
                        UseDefaultSpawns($"The server sent no data for {locationId}");
                    else
                        opened = OpenPicker(controller, location, mapDef, advance);
                }
            }
        }
        catch (TimeoutException)
        {
            UseDefaultSpawns($"The server took over {ServerTimeout.TotalSeconds:0} seconds to answer");
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
            SpawnChoiceStore.Clear();
        }
        finally
        {
            _fetching = false;
            UnlockButtons(buttons);
        }

        if (!opened && StillShowing(controller, locationId))
            advance();
    }

    private static bool OpenPicker(Controller controller, LocationSettings.Location location, MapDef mapDef, Action advance)
    {
        if (mapDef == null)
        {
            UseDefaultSpawns($"No DynamicMaps map for {location.Id}");
            return false;
        }

        var screen = controller.Screen;
        var side = controller.RaidSettings.Side;
        var playerSide = side == ESideType.Savage ? EPlayerSide.Savage : Profile(screen).Info.Side;
        if (SpawnPickerWindow.Open(mapDef, location, side, playerSide, screen, screen._acceptButton, advance))
            return true;

        UseDefaultSpawns("The spawn picker failed to open");
        return false;
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

    private static bool StillShowing(Controller controller, string locationId) =>
        controller.Active && string.Equals(controller.Screen.SelectedLocation?.Id, locationId, StringComparison.OrdinalIgnoreCase);

    private static (DefaultUIButton Button, bool Interactable)[] LockButtons(MatchMakerSelectionLocationScreen screen)
    {
        var buttons = new[] { screen._acceptButton, screen._readyButton }
            .Where(button => button != null)
            .Select(button => (button, button.Interactable))
            .ToArray();

        foreach (var (button, _) in buttons)
            button.Interactable = false;

        return buttons;
    }

    private static void UnlockButtons((DefaultUIButton Button, bool Interactable)[] buttons)
    {
        foreach (var (button, interactable) in buttons)
        {
            if (button != null)
                button.Interactable = interactable;
        }
    }

    private static async Task<T> WithTimeout<T>(Task<T> task)
    {
        using var timeout = new CancellationTokenSource();
        if (await Task.WhenAny(task, Task.Delay(ServerTimeout, timeout.Token)) != task)
            throw new TimeoutException();

        timeout.Cancel();
        return await task;
    }

    private static string Variant(bool backport) => backport ? "backport" : "original";

    private static void UseDefaultSpawns(string reason)
    {
        SpawnChoiceStore.Clear();
        Plugin.LogSource.LogInfo($"{reason}, using default spawns");
    }
}
