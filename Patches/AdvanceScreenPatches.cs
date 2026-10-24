using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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
    // Harmony keys patches by MethodInfo: reached through Controller, this method would get a detour apart from MapVariants' and override it
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Controller), nameof(Controller.ShowNextScreen))?.GetDeclaredMember();

    [PatchPrefix]
    [HarmonyPriority(Priority.Low)]
    public static bool Prefix(object __instance, bool __runOriginal) =>
        LocationScreenAdvance.Intercept(__instance, __runOriginal, ready: false);
}

public class ReadyScreenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Controller), nameof(Controller.ShowReadyScreen))?.GetDeclaredMember();

    [PatchPrefix]
    [HarmonyPriority(Priority.Low)]
    public static bool Prefix(object __instance, bool __runOriginal) =>
        LocationScreenAdvance.Intercept(__instance, __runOriginal, ready: true);
}

public static class LocationScreenAdvance
{
    private static readonly AccessTools.FieldRef<MatchMakerSelectionLocationScreen, Profile> Profile =
        AccessTools.FieldRefAccess<MatchMakerSelectionLocationScreen, Profile>("_profile");

    private static readonly Dictionary<string, LocationSettings.Location> OriginalLocations = new();

    // The patched method is shared by every screen controller of the sequence: __instance may be another one
    public static bool Intercept(object instance, bool runOriginal, bool ready)
    {
        if (instance is not Controller controller || !runOriginal)
            return true;

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

        string fallback;
        try
        {
            var opening = TryOpenPicker(controller, location, advance);
            if (!opening.IsCompleted)
                SpawnPickerWindow.OpenCover(controller.Screen, controller.Screen._acceptButton);
            fallback = await opening;
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
            fallback = "The spawn picker failed";
        }
        finally
        {
            SpawnPickerWindow.CloseCover();
        }

        if (fallback == null)
            return;

        SpawnChoiceStore.Clear();
        Plugin.LogSource.LogInfo($"{fallback}, using default spawns");
        advance();
    }

    private static async Task<string> TryOpenPicker(Controller controller, LocationSettings.Location location, Action advance)
    {
        var locationId = location.Id;
        if (controller.RaidSettings.isInTransition)
            return $"{locationId} is reached by transit";

        if (MapDefCatalog.Find(locationId) == null)
            return $"No DynamicMaps map for {locationId}";

        if (MapVariantsCompat.Manages(locationId))
        {
            // Straight from MapVariants' window, whose choice the server just acknowledged
            var backport = MapVariantsCompat.ChoseBackport(locationId) ?? await MapVariantsCompat.ServesBackport(locationId);
            if (backport == null)
                return $"The server did not say which {locationId} it serves";
            if (backport.Value)
                return $"No DynamicMaps map for the backport {locationId}";

            location = await OriginalLocation(location);
            if (location == null)
                return $"The server sent no data for {locationId}";
        }

        return OpenPicker(controller, location, locationId, advance);
    }

    private static string OpenPicker(Controller controller, LocationSettings.Location location, string locationId, Action advance)
    {
        var mapDef = MapDefCatalog.Find(locationId);
        if (mapDef == null)
            return $"No DynamicMaps map for {locationId}";

        var screen = controller.Screen;
        var side = controller.RaidSettings.Side;
        var playerSide = side == ESideType.Savage ? EPlayerSide.Savage : Profile(screen).Info.Side;
        SpawnPickerWindow.Open(mapDef, location, side, playerSide, screen, screen._acceptButton, advance);
        return null;
    }

    private static async Task<LocationSettings.Location> OriginalLocation(LocationSettings.Location selected)
    {
        if (OriginalLocations.TryGetValue(selected.Id, out var location))
            return location;

        location = await ServerLocations.Fetch(selected._Id);
        if (location != null)
            OriginalLocations[selected.Id] = location;

        return location;
    }
}
