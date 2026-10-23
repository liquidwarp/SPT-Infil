using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        LocationScreenAdvance.Declared(nameof(Controller.ShowNextScreen));

    [PatchPrefix]
    [HarmonyPriority(Priority.Low)]
    public static bool Prefix(object __instance, bool __runOriginal) =>
        LocationScreenAdvance.Intercept(__instance, __runOriginal, ready: false);
}

public class ReadyScreenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        LocationScreenAdvance.Declared(nameof(Controller.ShowReadyScreen));

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

    private static bool _fetching;

    // Harmony keys patches by MethodInfo: reached through Controller, this method would get a detour apart from MapVariants' and override it
    public static MethodInfo Declared(string name) =>
        AccessTools.DeclaredMethod(AccessTools.Method(typeof(Controller), name).DeclaringType, name);

    // The patched method is shared by every screen controller of the sequence: __instance may be another one
    public static bool Intercept(object instance, bool runOriginal, bool ready)
    {
        if (instance is not Controller controller || !runOriginal)
            return true;

        if (_fetching)
            return false;

        var location = controller.Screen.SelectedLocation;
        if (location == null)
            return true;

        Offer(controller, location, ready);
        return false;
    }

    private static async void Offer(Controller controller, LocationSettings.Location location, bool ready)
    {
        // The original only invokes this, and calling it directly keeps MapVariants from prompting again
        Action advance = () => (ready ? controller._onShowReadyScreen : controller._onShowNextScreen)?.Invoke();

        _fetching = true;
        (DefaultUIButton, bool)[] buttons = [];
        string fallback;
        try
        {
            buttons = LockButtons(controller.Screen);
            fallback = await TryOpenPicker(controller, location, advance);
        }
        catch (TimeoutException)
        {
            fallback = $"The server took over {ServerTimeout.TotalSeconds:0} seconds to answer";
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
            fallback = "The spawn picker failed";
        }
        finally
        {
            _fetching = false;
            UnlockButtons(buttons);
        }

        if (fallback == null)
            return;

        SpawnChoiceStore.Clear();
        Plugin.LogSource.LogInfo($"{fallback}, using default spawns");
        advance();
    }

    /// Null when the picker opened, or when the player left the map while the server answered
    private static async Task<string> TryOpenPicker(Controller controller, LocationSettings.Location location, Action advance)
    {
        var locationId = location.Id;
        if (controller.RaidSettings.isInTransition)
            return $"{locationId} is reached by transit";

        var mapDef = MapDefCatalog.Find(locationId);
        if (MapVariantsMaps.Manages(locationId))
        {
            // Straight from MapVariants' window, whose choice the server just acknowledged
            var chosen = MapVariantsMaps.ChoseBackport(locationId);
            var backport = chosen;
            if (backport == null)
            {
                backport = await WithTimeout(MapVariantsMaps.ServesBackport(locationId));
                if (!StillShowing(controller, locationId))
                    return null;

                if (backport == null)
                    return $"The server did not say which {locationId} it serves";
            }

            if (backport.Value)
                mapDef = DynamicMapsExtendedMaps.FindBackport(locationId);
            if (mapDef == null)
                return $"No DynamicMaps map for the {Variant(backport.Value)} {locationId}";

            var cover = chosen != null ? SpawnPickerWindow.OpenCover() : null;
            try
            {
                location = await VariantLocation(location, backport.Value);
            }
            finally
            {
                if (cover != null)
                    UnityEngine.Object.Destroy(cover);
            }

            if (!StillShowing(controller, locationId))
                return null;

            if (location == null)
                return $"The server sent no data for {locationId}";
        }

        if (mapDef == null)
            return $"No DynamicMaps map for {locationId}";

        var screen = controller.Screen;
        var side = controller.RaidSettings.Side;
        var playerSide = side == ESideType.Savage ? EPlayerSide.Savage : Profile(screen).Info.Side;
        return SpawnPickerWindow.Open(mapDef, location, side, playerSide, screen, screen._acceptButton, advance)
            ? null
            : "The spawn picker failed to open";
    }

    private static async Task<LocationSettings.Location> VariantLocation(LocationSettings.Location selected, bool backport)
    {
        var key = (selected.Id, backport);
        if (VariantLocations.TryGetValue(key, out var location))
            return location;

        location = await WithTimeout(ServerLocations.Fetch(selected._Id));
        if (location != null)
            VariantLocations[key] = location;

        return location;
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

    private static async Task<T> WithTimeout<T>(Task<T> task) =>
        await Task.WhenAny(task, Task.Delay(ServerTimeout)) == task ? await task : throw new TimeoutException();

    private static string Variant(bool backport) => backport ? "backport" : "original";
}
