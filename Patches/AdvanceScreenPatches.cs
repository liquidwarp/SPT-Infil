using System;
using System.Reflection;
using EFT;
using EFT.UI.Matchmaker;
using HarmonyLib;
using Liquidwarp.Infil.Compat;
using Liquidwarp.Infil.Map;
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
        LocationScreenAdvance.Intercept(__instance, __runOriginal, controller => controller.ShowNextScreen());
}

public class ReadyScreenPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(Controller), nameof(Controller.ShowReadyScreen));

    [PatchPrefix]
    [HarmonyPriority(Priority.Low)]
    public static bool Prefix(object __instance, bool __runOriginal) =>
        LocationScreenAdvance.Intercept(__instance, __runOriginal, controller => controller.ShowReadyScreen());
}

public static class LocationScreenAdvance
{
    private static readonly AccessTools.FieldRef<MatchMakerSelectionLocationScreen, Profile> Profile =
        AccessTools.FieldRefAccess<MatchMakerSelectionLocationScreen, Profile>("_profile");

    private static bool _passThrough;

    public static bool Intercept(object instance, bool runOriginal, Action<Controller> advance)
    {
        if (!runOriginal || instance is not Controller controller)
            return true;

        if (_passThrough)
        {
            _passThrough = false;
            return true;
        }

        try
        {
            var screen = controller.Screen;
            var location = screen.SelectedLocation;
            var raidSettings = controller.RaidSettings;
            if (location == null || raidSettings.isInTransition)
                return true;

            if (MapVariantsMaps.Manages(location.Id))
            {
                Plugin.LogSource.LogInfo($"MapVariants manages {location.Id}, using vanilla spawns");
                return true;
            }

            if (MapDefCatalog.Find(location.Id) == null)
            {
                Plugin.LogSource.LogInfo($"No DynamicMaps map for {location.Id}, using vanilla spawns");
                return true;
            }

            var side = raidSettings.Side;
            var playerSide = side == ESideType.Savage ? EPlayerSide.Savage : Profile(screen).Info.Side;
            return !SpawnPickerWindow.Open(location, side, playerSide, screen, screen._acceptButton, () =>
            {
                _passThrough = true;
                advance(controller);
            });
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
            return true;
        }
    }
}
