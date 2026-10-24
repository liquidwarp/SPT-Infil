using System.Linq;
using System.Reflection;
using EFT;
using EFT.UI.Matchmaker;
using HarmonyLib;
using Liquidwarp.Infil.Spawn;
using Liquidwarp.Infil.UI;
using SPT.Reflection.Patching;
using TMPro;
using UnityEngine;

namespace Liquidwarp.Infil.Patches;

public class ConfirmationScreenPatch : ModulePatch
{
    private static TextMeshProUGUI _text;

    protected override MethodBase GetTargetMethod() =>
        AccessTools.GetDeclaredMethods(typeof(MatchMakerAcceptScreen))
            .First(method => method.Name == nameof(MatchMakerAcceptScreen.Show) && method.GetParameters().Length == 3);

    [PatchPostfix]
    public static void Postfix(MatchMakerAcceptScreen __instance, RaidSettings raidSettings)
    {
        try
        {
            if (_text == null)
                _text = CreateText(__instance._locationName);

            var spawnArea = Describe(raidSettings);
            _text.gameObject.SetActive(spawnArea != null);
            _text.text = spawnArea;
        }
        catch (System.Exception ex)
        {
            Plugin.LogSource.LogError($"Could not show the chosen spawn area: {ex}");
        }
    }

    public static void RemoveText()
    {
        if (_text != null)
            Object.Destroy(_text.gameObject);
    }

    private static string Describe(RaidSettings raidSettings)
    {
        var spot = SpawnChoiceStore.Get(raidSettings.SelectedLocation?.Id, raidSettings.Side);
        if (spot == null)
            return null;

        return spot.Where != null ? $"{spot.Title}, {spot.Where}" : spot.Title;
    }

    private static TextMeshProUGUI CreateText(TextMeshProUGUI locationName)
    {
        var text = SpawnPickerWindow.CreateText(locationName.gameObject, "InfilSpawnArea", locationName.font, 20f, FontStyles.Normal);
        text.color = new Color(0.7f, 0.7f, 0.7f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;

        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, -12f);
        rect.sizeDelta = new Vector2(600f, 30f);
        return text;
    }
}
