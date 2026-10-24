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
using Liquidwarp.Infil.Spawn;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Liquidwarp.Infil.UI;

public class SpawnPickerWindow : InputNode
{
    private const string MarkerCategory = "Infil";

    private const string GroupNote =
        "\n\n<color=#B3B3B3><i>With Fika, in a group set to spawn together, everyone spawns at the host's location regardless of your choice here.</i></color>";

    private static readonly Vector2 MapSize = new(1320f, 940f);
    private static readonly Vector2 PanelSize = new(460f, 940f);
    private static readonly Vector2 MarkerSize = new(64f, 64f);
    private static readonly Vector2 SelectedMarkerSize = new(80f, 80f);
    private static readonly Vector2 PulseSize = new(128f, 128f);
    private const float PulsePeriod = 1.4f;
    private const int DotTextureSize = 128;
    private const float DotFillRadius = 24f;
    private const float DotEdgeRadius = 28f;
    private const float DotRadius = DotEdgeRadius / DotTextureSize;
    private const float MapDim = 0.5f;
    private const float OtherLevelAlpha = 0.4f;

    private static readonly Color MarkerColor = new(0.30f, 0.75f, 1.00f);

    private static SpawnPickerWindow _instance;
    private static GameObject _cover;
    private static Sprite _dotSprite;
    private static Sprite _ringSprite;

    private readonly List<SpawnMarker> _markers = [];
    private Image _pulse;
    private float _pulseStart;
    private LocationSettings.Location _location;
    private ESideType _side;
    private DefaultUIButton _ownerButton;
    private Action _onNext;
    private InputNode _screen;
    private TextMeshProUGUI _infoText;
    private SpawnMarker _selected;

    private class SpawnMarker
    {
        public SpawnSpot Spot;
        public Transform Marker;
        public Image Image;
        public string Levels;
    }

    private static void ShowMarkerCategory()
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

    public static void Open(MapDef mapDef, LocationSettings.Location location, List<SpawnSpot> spots, ESideType side, InputNode screen, DefaultUIButton ownerButton, Action onNext)
    {
        CloseOpen();

        var root = CreateCanvas("InfilPicker");
        _instance = root.AddComponent<SpawnPickerWindow>();
        _instance._location = location;
        _instance._side = side;
        _instance._ownerButton = ownerButton;
        _instance._onNext = onNext;

        // Child input nodes get keys before their parent: we see Escape before the location screen does
        _instance._screen = screen;
        screen.Add(_instance);

        try
        {
            _instance.Build(mapDef, spots);
        }
        catch
        {
            Destroy(root);
            throw;
        }
    }

    public static void CloseOpen()
    {
        if (_instance != null)
            Destroy(_instance.gameObject);
    }

    public static void OpenCover(InputNode screen, DefaultUIButton ownerButton)
    {
        CloseCover();
        _cover = CreateCanvas("InfilCover");
        var cover = _cover.AddComponent<Cover>();
        cover.Screen = screen;
        screen.Add(cover);

        var text = CreateText(CreateBackground(_cover), "Loading", HeaderFont(ownerButton), 24f, FontStyles.Normal);
        text.text = "LOADING SPAWN POINTS...";
        text.rectTransform.sizeDelta = new Vector2(800f, 100f);
    }

    public static void CloseCover()
    {
        if (_cover != null)
            Destroy(_cover);
        _cover = null;
    }

    private class Cover : InputNode
    {
        public InputNode Screen;

        public override ETranslateResult TranslateCommand(ECommand command) => ETranslateResult.BlockAll;

        public override void TranslateAxes(ref float[] axes)
        {
        }

        public override ECursorResult ShouldLockCursor() => ECursorResult.ShowCursor;

        private void OnDestroy()
        {
            if (Screen != null)
                Screen.Remove(this);
        }
    }

    private static TMP_FontAsset HeaderFont(DefaultUIButton button) =>
        button._headerLabel != null ? button._headerLabel.font : null;

    private static GameObject CreateCanvas(string name)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
        root.AddComponent<GraphicRaycaster>();
        return root;
    }

    private static GameObject CreateBackground(GameObject parent)
    {
        var background = UIUtils.CreateUIGameObject(parent, "Background");
        var backgroundRect = background.GetRectTransform();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
        background.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);
        return background;
    }

    private void Build(MapDef mapDef, List<SpawnSpot> spots)
    {
        ShowMarkerCategory();
        var background = CreateBackground(gameObject);

        BuildMap(background, mapDef, spots);
        BuildPanel(background);

        var chosen = SpawnChoiceStore.Get(_location.Id, _side);
        Select(chosen != null ? _markers.FirstOrDefault(marker => marker.Spot.PointIds.SetEquals(chosen.PointIds)) : null);
    }

    private void BuildMap(GameObject parent, MapDef mapDef, List<SpawnSpot> spots)
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

        // LoadMap zooms out to fit the whole map in the area. Without the map's own markers (extracts, transits...): we show only ours
        var mapView = MapView.Create(mapArea, "MapView");
        var staticMarkers = mapDef.StaticMarkers;
        mapDef.StaticMarkers = [];
        try
        {
            mapView.LoadMap(mapDef, mapAreaRect);
        }
        finally
        {
            mapDef.StaticMarkers = staticMarkers;
        }

        var layers = mapView.MapLayerContainer.GetComponentsInChildren<MapLayer>(true);
        if (layers.Length > 0)
        {
            var topLevel = int.MinValue;
            foreach (var layer in layers)
                topLevel = Mathf.Max(topLevel, layer.Level);
            mapView.SelectTopLevel(topLevel);
        }
        ShowAllLevels(mapView, layers);

        // Dims the map and its labels, under our markers; the mask clips it to the map area
        var dim = UIUtils.CreateUIGameObject(mapView.gameObject, "Dim");
        dim.GetRectTransform().sizeDelta = new Vector2(100000f, 100000f);
        dim.transform.SetSiblingIndex(mapView.MapMarkerContainer.transform.GetSiblingIndex());
        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, MapDim);
        dimImage.raycastTarget = false;

        _pulse = UIUtils.CreateUIGameObject(mapView.MapMarkerContainer, "Pulse").AddComponent<Image>();
        _pulse.sprite = GetRingSprite();
        _pulse.raycastTarget = false;
        _pulse.rectTransform.sizeDelta = PulseSize;
        _pulse.color = MarkerColor;
        // Its own canvas, so animating it doesn't rebuild the whole picker every frame
        _pulse.gameObject.AddComponent<Canvas>();
        _pulse.gameObject.SetActive(false);

        foreach (var spot in spots)
        {
            var marker = mapView.AddMapMarker(new MapMarkerDef
            {
                Text = spot.Label.TrimStart('#'),
                Category = MarkerCategory,
                Sprite = GetDotSprite(),
                Position = MathUtils.ConvertToMapPosition(spot.Position),
                Color = MarkerColor,
            });
            marker.Size = MarkerSize;
            marker.Label.rectTransform.anchoredPosition = new Vector2(0f, MarkerSize.y / 2f - SelectedMarkerSize.y * DotRadius);

            var spawnMarker = new SpawnMarker { Spot = spot, Marker = marker.transform, Image = marker.Image, Levels = DescribeLevels(layers, spot) };
            marker.gameObject.AddComponent<ClickTrigger>().Init(eventData =>
            {
                if (eventData.button == PointerEventData.InputButton.Left)
                    Select(spawnMarker);
            });
            _markers.Add(spawnMarker);
        }
    }

    private void BuildPanel(GameObject parent)
    {
        var font = HeaderFont(_ownerButton);

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

    private void AddButton(GameObject parent, string text, float bottom, UnityAction onClick)
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
        button.OnClick.AddListener(onClick);
    }

    private void Select(SpawnMarker spawnMarker)
    {
        if (_selected != null)
            SetMarkerLook(_selected, selected: false);

        _selected = spawnMarker;
        _pulse.gameObject.SetActive(_selected != null);
        if (_selected == null)
        {
            _infoText.text = "<b>Default: Random spawn</b>\n\nYou spawn at a random spawn point, as usual." + GroupNote;
            return;
        }

        SetMarkerLook(_selected, selected: true);
        _selected.Marker.SetAsLastSibling();
        _pulse.transform.SetParent(_selected.Marker, false);
        _pulse.transform.SetAsFirstSibling();
        _pulse.rectTransform.anchoredPosition = Vector2.zero;
        _pulseStart = Time.unscaledTime;
        UpdatePulse();

        var where = _selected.Spot.Where;
        var lines = new List<string> { $"<b>Selected: {_selected.Spot.Label}</b>" };
        if (where != null)
            lines.Add(char.ToUpper(where[0]) + where.Substring(1));

        lines.Add("");
        lines.Add(_selected.Levels != null
            ? $"Possible spawn points:\n<indent=20>{_selected.Levels}</indent>"
            : $"Possible spawn points: {_selected.Spot.PointIds.Count}");

        _infoText.text = string.Join("\n", lines) + GroupNote;
    }

    private static void SetMarkerLook(SpawnMarker spawnMarker, bool selected) =>
        spawnMarker.Image.rectTransform.sizeDelta = selected ? SelectedMarkerSize : MarkerSize;

    private static void ShowAllLevels(MapView mapView, MapLayer[] layers)
    {
        // The default level is opaque, so it goes at the bottom with the other levels see-through above it.
        var order = new (int NotDefault, int Level, int Index)[layers.Length];
        for (var i = 0; i < layers.Length; i++)
            order[i] = (layers[i].IsOnDefaultLevel ? 0 : 1, layers[i].Level, i);
        Array.Sort(order);
        foreach (var (_, _, index) in order)
        {
            var layer = layers[index];
            layer.gameObject.SetActive(true);
            layer.transform.SetAsLastSibling();
            layer.GetComponent<Graphic>().color = new Color(1f, 1f, 1f, layer.IsOnDefaultLevel ? 1f : OtherLevelAlpha);
        }

        foreach (var label in mapView.MapLabelsContainer.GetComponentsInChildren<MapLabel>(true))
        {
            label.LabelAlphaLayerStatus = MapMarker.CategoryLabelAlphaLayerStatus[MarkerCategory];
            label.HandleNewLayerStatus(LayerStatus.OnTop);
        }
    }

    private static string DescribeLevels(MapLayer[] layers, SpawnSpot spot)
    {
        if (layers.Length == 0)
            return null;

        MapLayer defaultLayer = null;
        foreach (var layer in layers)
        {
            if (layer.IsOnDefaultLevel)
            {
                defaultLayer = layer;
                break;
            }
        }

        var counts = new Dictionary<MapLayer, int>();
        var others = 0;
        foreach (var position in spot.PointPositions)
        {
            var layer = FindLayer(layers, MathUtils.ConvertToMapPosition(position)) ?? defaultLayer;
            if (layer == null)
                others++;
            else
                counts[layer] = counts.TryGetValue(layer, out var count) ? count + 1 : 1;
        }

        var lines = new List<(int Level, string Text)>();
        foreach (var (layer, count) in counts)
            lines.Add((layer.Level, $"{layer.name}: {count}"));
        if (others > 0)
            lines.Add((int.MaxValue, $"Other: {others}"));
        lines.Sort();

        return string.Join("\n", lines.Select(line => line.Text));
    }

    private static MapLayer FindLayer(MapLayer[] layers, Vector3 position)
    {
        MapLayer best = null;
        var bestVolume = float.MaxValue;
        foreach (var layer in layers)
        {
            if (!layer.IsCoordinateInLayer(position))
                continue;

            var volume = layer.GetMatchingBoundVolume(position);
            if (volume < bestVolume)
                (best, bestVolume) = (layer, volume);
        }

        return best;
    }

    private static Sprite GetDotSprite()
    {
        if (_dotSprite == null)
        {
            _dotSprite = CreateRoundSprite(DotTextureSize, distance =>
            {
                var fill = Coverage(DotFillRadius - distance);
                var alpha = Mathf.Lerp(0.6f, 1f, fill) * Coverage(DotEdgeRadius - distance);
                return new Color(fill, fill, fill, alpha);
            });
        }

        return _dotSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (_ringSprite == null)
        {
            const int size = 256;
            _ringSprite = CreateRoundSprite(size, distance =>
                new Color(1f, 1f, 1f, Coverage(6f - Mathf.Abs(distance - (size / 2f - 10f)))));
        }

        return _ringSprite;
    }

    private static float Coverage(float insideDistance) => Mathf.Clamp01(insideDistance + 0.5f);

    private static Sprite CreateRoundSprite(int size, Func<float, Color> shade)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };

        var pixels = new Color[size * size];
        var center = (size - 1) / 2f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
            pixels[y * size + x] = shade(new Vector2(x - center, y - center).magnitude);

        texture.SetPixels(pixels);
        texture.Apply(true, true);

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
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
        {
            Close();
            return;
        }

        if (Input.GetMouseButtonDown(1))
            Select(null);

        if (_selected != null)
            UpdatePulse();
    }

    private void UpdatePulse()
    {
        var t = (Time.unscaledTime - _pulseStart) % PulsePeriod / PulsePeriod;
        _pulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, 1f - (1f - t) * (1f - t));
        _pulse.canvasRenderer.SetAlpha(0.9f * (1f - t));
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

    internal static TextMeshProUGUI CreateText(GameObject parent, string name, TMP_FontAsset font, float size, FontStyles style)
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
