using System;
using System.Collections.Generic;
using System.Linq;
using DynamicMaps.Data;
using DynamicMaps.UI.Components;
using DynamicMaps.Utils;
using EFT;
using EFT.InputSystem;
using EFT.UI;
using JsonType;
using Liquidwarp.SpawnSelector.Spawn;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liquidwarp.SpawnSelector.UI;

public class SpawnPickerWindow : InputNode
{
    private const string MarkerCategory = "SpawnSelector";
    private const string MarkerImage = "Markers/dot.png";
    private const string SelectedMarkerImage = "Markers/star.png";

    private static readonly Vector2 MapSize = new(1320f, 940f);
    private static readonly Vector2 PanelSize = new(460f, 940f);
    private static readonly Vector2 MarkerSize = new(180f, 180f);
    private static readonly Vector2 SelectedMarkerSize = new(53f, 53f);
    private const float DotRadius = 5f / 72f;
    private const float MapDim = 0.5f;

    private static readonly Color MarkerColor = new(0.30f, 0.75f, 1.00f);

    private static SpawnPickerWindow _instance;

    private readonly List<SpawnMarker> _markers = [];
    private LocationSettings.Location _location;
    private ESideType _side;
    private EPlayerSide _playerSide;
    private DefaultUIButton _ownerButton;
    private Action _onNext;
    private InputNode _screen;
    private TextMeshProUGUI _infoText;
    private SpawnMarker _selected;

    private class SpawnMarker
    {
        public SpawnSpot Spot;
        public MapMarker Marker;
    }

    static SpawnPickerWindow()
    {
        var alwaysShown = new Dictionary<LayerStatus, float>
        {
            [LayerStatus.Hidden] = 1f,
            [LayerStatus.Underneath] = 1f,
            [LayerStatus.OnTop] = 1f,
            [LayerStatus.FullReveal] = 1f,
        };
        MapMarker.CategoryImageAlphaLayerStatus[MarkerCategory] = alwaysShown;
        MapMarker.CategoryLabelAlphaLayerStatus[MarkerCategory] = alwaysShown;
    }

    public static void Open(LocationSettings.Location location, ESideType side, EPlayerSide playerSide, InputNode screen, DefaultUIButton ownerButton, Action onNext)
    {
        if (_instance != null)
            Destroy(_instance.gameObject);

        var mapDef = MapDefCatalog.Find(location.Id);
        if (mapDef == null)
            return;

        var root = new GameObject("SpawnSelectorPicker", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
        root.AddComponent<GraphicRaycaster>();

        _instance = root.AddComponent<SpawnPickerWindow>();
        _instance._location = location;
        _instance._side = side;
        _instance._playerSide = playerSide;
        _instance._ownerButton = ownerButton;
        _instance._onNext = onNext;

        // Child input nodes get keys before their parent: we see Escape before the location screen does
        _instance._screen = screen;
        screen.Add(_instance);

        try
        {
            _instance.Build(mapDef);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogError($"Could not open the spawn picker: {ex}");
            Destroy(root);
        }
    }

    private void Build(MapDef mapDef)
    {
        var background = UIUtils.CreateUIGameObject(gameObject, "Background");
        var backgroundRect = background.GetRectTransform();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        background.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);

        BuildMap(background, mapDef);
        BuildPanel(background);

        var label = SpawnChoiceStore.Get(_location.Id, _side)?.Label;
        Select(_markers.FirstOrDefault(marker => marker.Spot.Label == label));
    }

    private void BuildMap(GameObject parent, MapDef mapDef)
    {
        var mapArea = UIUtils.CreateUIGameObject(parent, "MapArea");
        var mapAreaRect = mapArea.GetRectTransform();
        mapAreaRect.sizeDelta = MapSize;
        mapAreaRect.anchoredPosition = new Vector2(-(PanelSize.x + 20f) / 2f, 0f);

        mapArea.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        mapArea.AddComponent<Mask>();
        mapArea.AddComponent<ClickTrigger>().Init(eventData =>
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Select(null);
        });

        // LoadMap zooms out to fit the whole map in the area
        var mapView = MapView.Create(mapArea, "MapView");
        mapView.LoadMap(mapDef, mapAreaRect);

        // Dims the map and its labels, under our markers; the mask clips it to the map area
        var dim = UIUtils.CreateUIGameObject(mapView.gameObject, "Dim");
        dim.GetRectTransform().sizeDelta = new Vector2(100000f, 100000f);
        dim.transform.SetSiblingIndex(mapView.MapMarkerContainer.transform.GetSiblingIndex());
        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, MapDim);
        dimImage.raycastTarget = false;

        foreach (var spot in SpawnSpots.For(_location, _playerSide))
        {
            var marker = mapView.AddMapMarker(new MapMarkerDef
            {
                Text = spot.Label,
                Category = MarkerCategory,
                ImagePath = MarkerImage,
                Position = MathUtils.ConvertToMapPosition(spot.Position),
                Color = MarkerColor,
            });

            var spawnMarker = new SpawnMarker { Spot = spot, Marker = marker };
            marker.gameObject.AddComponent<ClickTrigger>().Init(eventData =>
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                    Select(spawnMarker);
            });
            SetMarkerLook(spawnMarker, selected: false);
            _markers.Add(spawnMarker);
        }
    }

    private void BuildPanel(GameObject parent)
    {
        var font = _ownerButton._headerLabel != null ? _ownerButton._headerLabel.font : null;

        var panel = UIUtils.CreateUIGameObject(parent, "Panel");
        var panelRect = panel.GetRectTransform();
        panelRect.sizeDelta = PanelSize;
        panelRect.anchoredPosition = new Vector2((MapSize.x + 20f) / 2f, 0f);
        panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        var title = CreateText(panel, "Title", font, 30f, FontStyles.Bold);
        title.text = $"CHOOSE YOUR SPAWN\n<size=60%>{_location.LocalizedName} - {(_side == ESideType.Savage ? "Scav" : "PMC")}</size>";
        SetTop(title.rectTransform, 20f, 90f);

        var help = CreateText(panel, "Help", font, 16f, FontStyles.Italic);
        help.text = "Select a blue dot to pick where you spawn.";
        help.color = new Color(0.7f, 0.7f, 0.7f);
        SetTop(help.rectTransform, 115f, 30f);

        _infoText = CreateText(panel, "Info", font, 20f, FontStyles.Normal);
        _infoText.alignment = TextAlignmentOptions.TopLeft;
        var infoRect = _infoText.rectTransform;
        infoRect.anchorMin = new Vector2(0f, 0f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.offsetMin = new Vector2(25f, 170f);
        infoRect.offsetMax = new Vector2(-25f, -160f);

        AddButton(panel, "CONFIRM", 90f, Next);
        AddButton(panel, "BACK", 30f, Close);
    }

    private void AddButton(GameObject parent, string text, float bottom, Action onClick)
    {
        var buttonObject = Instantiate(_ownerButton.gameObject, parent.transform, false);
        buttonObject.name = text;
        buttonObject.SetActive(true);

        var rect = buttonObject.GetRectTransform();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottom);

        var button = buttonObject.GetComponent<DefaultUIButton>();
        button.Interactable = true;
        button.SetRawText(text, 22);
        button.OnClick.RemoveAllListeners();
        button.OnClick.AddListener(() => onClick());
    }

    private void Select(SpawnMarker spawnMarker)
    {
        if (_selected != null)
            SetMarkerLook(_selected, selected: false);

        _selected = spawnMarker;

        if (_selected == null)
        {
            _infoText.text = "<b>Fika's choice</b>\n\nFika spawns you as usual: with the host when the group spawns together, otherwise at a random spawn point.";
            return;
        }

        SetMarkerLook(_selected, selected: true);
        _infoText.text = $"<b>Spawn {_selected.Spot.Label}</b>\n\nSpawn points here: {_selected.Spot.PointIds.Count}";
    }

    private static void SetMarkerLook(SpawnMarker spawnMarker, bool selected)
    {
        var marker = spawnMarker.Marker;
        marker.Image.sprite = TextureUtils.GetOrLoadCachedSprite(selected ? SelectedMarkerImage : MarkerImage);
        marker.Size = selected ? SelectedMarkerSize : MarkerSize;

        marker.Label.rectTransform.anchoredPosition = new Vector2(0f, selected ? 0f : MarkerSize.y * (0.5f - DotRadius));

        if (selected)
            marker.transform.SetAsLastSibling();
    }

    private void Next()
    {
        SpawnChoiceStore.Set(_location.Id, _side, _selected?.Spot);
        Close();
        _onNext?.Invoke();
    }

    private void Close() => Destroy(gameObject);

    private void Update()
    {
        if (_ownerButton == null || !_ownerButton.isActiveAndEnabled)
            Close();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        if (_screen != null)
            _screen.Remove(this);
    }

    public override ETranslateResult TranslateCommand(ECommand command)
    {
        if (command.IsCommand(ECommand.Escape))
        {
            Close();
            return ETranslateResult.BlockAll;
        }

        return GetDefaultBlockResult(command);
    }

    public override void TranslateAxes(ref float[] axes)
    {
    }

    public override ECursorResult ShouldLockCursor() => ECursorResult.ShowCursor;

    private static void SetTop(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(25f, -top - height);
        rect.offsetMax = new Vector2(-25f, -top);
    }

    private static TextMeshProUGUI CreateText(GameObject parent, string name, TMP_FontAsset font, float size, FontStyles style)
    {
        var text = UIUtils.CreateUIGameObject(parent, name).AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }
}
