using System;
using System.Reflection;
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

public class AdvanceScreenPatch(bool ready) : ModulePatch
{
    // Harmony keys patches by MethodInfo: reached through Controller, this method would get a detour apart from MapVariants' and override it
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Controller), ready ? nameof(Controller.ShowReadyScreen) : nameof(Controller.ShowNextScreen))?.GetDeclaredMember();

    [PatchPrefix]
    [HarmonyPriority(Priority.Low)]
    public static bool Prefix(object __instance, bool __runOriginal, MethodBase __originalMethod) =>
        LocationScreenAdvance.Intercept(__instance, __runOriginal, ready: __originalMethod.Name == nameof(Controller.ShowReadyScreen));
}

public static class LocationScreenAdvance
{
    // The patched method is shared by every screen controller of the sequence: __instance may be another one
    public static bool Intercept(object instance, bool runOriginal, bool ready)
    {
        if (instance is not Controller controller || !runOriginal)
            return true;

        try
        {
            var location = controller.Screen.SelectedLocation;
            if (location == null)
                return true;

            Offer(controller, location, ready);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not offer the spawn picker, using default spawns: {ex}");
            SpawnChoiceStore.Clear();
            return true;
        }
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
        try
        {
            advance();
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not advance from the location screen: {ex}");
        }
    }

    private static async Task<string> TryOpenPicker(Controller controller, LocationSettings.Location location, Action advance)
    {
        var locationId = location.Id;
        if (controller.RaidSettings.isInTransition)
            return $"{locationId} is reached by transit";

        object mapDef = MapDefCatalog.Find(locationId);
        if (mapDef == null)
            return $"No DynamicMaps map for {locationId}";

        if (MapVariantsCompat.Manages(locationId))
        {
            // Straight from MapVariants' window, whose choice the server just acknowledged
            var backport = MapVariantsCompat.ChoseBackport(locationId) ?? await MapVariantsCompat.ServesBackport(locationId);
            if (backport == null)
                return $"The server did not say which {locationId} it serves";
            if (backport.Value)
                return $"No DynamicMaps map for the backport {locationId}";

            location = await ServerLocations.Fetch(location._Id);
            if (location == null)
                return $"The server sent no data for {locationId}";
        }

        return OpenPicker(controller, location, (MapDef)mapDef, advance);
    }

    private static string OpenPicker(Controller controller, LocationSettings.Location location, MapDef mapDef, Action advance)
    {
        var screen = controller.Screen;
        var side = controller.RaidSettings.Side;
        var playerSide = side == ESideType.Savage ? EPlayerSide.Savage : controller.Session.Profile.Info.Side;
        var spots = SpawnSpots.For(location, playerSide, mapDef);
        if (spots.Count == 0)
            return $"No {playerSide} spawn point on {location.Id}";

        SpawnPickerWindow.Open(mapDef, location, spots, side, screen, screen._acceptButton, advance);
        return null;
    }
}
