using System.Reflection;
using EFT;
using EFT.UI.Matchmaker;
using HarmonyLib;
using Liquidwarp.SpawnSelector.UI;
using SPT.Reflection.Patching;

namespace Liquidwarp.SpawnSelector.Patches;

public class NextButtonPatch : ModulePatch
{
    private static readonly AccessTools.FieldRef<MatchMakerSelectionLocationScreen, MatchMakerSelectionLocationScreen.SelectionLocationScreenController> ScreenController =
        AccessTools.FieldRefAccess<MatchMakerSelectionLocationScreen, MatchMakerSelectionLocationScreen.SelectionLocationScreenController>("ScreenController");

    private static readonly AccessTools.FieldRef<MatchMakerSelectionLocationScreen, RaidSettings> RaidSettings =
        AccessTools.FieldRefAccess<MatchMakerSelectionLocationScreen, RaidSettings>("_raidSettings");

    private static readonly AccessTools.FieldRef<MatchMakerSelectionLocationScreen, Profile> Profile =
        AccessTools.FieldRefAccess<MatchMakerSelectionLocationScreen, Profile>("_profile");

    protected override MethodBase GetTargetMethod() =>
        AccessTools.Method(typeof(MatchMakerSelectionLocationScreen), nameof(MatchMakerSelectionLocationScreen.Awake));

    [PatchPostfix]
    public static void Postfix(MatchMakerSelectionLocationScreen __instance)
    {
        __instance._acceptButton.OnClick.RemoveAllListeners();
        __instance._acceptButton.OnClick.AddListener(() => OnNext(__instance));
    }

    private static void OnNext(MatchMakerSelectionLocationScreen screen)
    {
        try
        {
            var location = screen.SelectedLocation;
            var raidSettings = RaidSettings(screen);
            if (location != null && !raidSettings.isInTransition && MapDefCatalog.Find(location.Id) != null)
            {
                var side = raidSettings.Side;
                var playerSide = side == ESideType.Savage ? EPlayerSide.Savage : Profile(screen).Info.Side;
                SpawnPickerWindow.Open(location, side, playerSide, screen, screen._acceptButton, () => ScreenController(screen).ShowNextScreen());
                return;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
        }

        ScreenController(screen).ShowNextScreen();
    }
}
