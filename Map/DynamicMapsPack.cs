using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using DynamicMaps.Data;
using DynamicMaps.UI.Components;
using DynamicMaps.UI.Controls;
using EFT.UI;
using Liquidwarp.Infil.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Liquidwarp.Infil.Map;

public sealed class DynamicMapsPack : IMapPack
{
    // Stored as object so the class layout never names a DynamicMaps type (Unity JSON load safety).
    private readonly object _mapDef;

    private DynamicMapsPack(object mapDef) => _mapDef = mapDef;

    private MapDef Def => (MapDef)_mapDef;

    public string SourceName => "DynamicMaps";
    public float CoordinateRotation => Def.CoordinateRotation;
    public Vector2 BoundsMin => Def.Bounds.Min;
    public Vector2 BoundsMax => Def.Bounds.Max;

    public static IMapPack Find(string locationId)
    {
        var plugin = DynamicMaps.Plugin.Instance;
        if (locationId == null || plugin == null)
            return null;

        var mapScreen = plugin.Map;
        if (mapScreen == null)
            return null;

        var dropdown = mapScreen.GetComponentInChildren<MapSelectDropdown>(true);
        if (dropdown == null)
            return null;

        var screen = mapScreen.transform;
        var parent = screen.parent;
        var wasActive = mapScreen.gameObject.activeSelf;
        screen.SetParent(Singleton<CommonUI>.Instance.transform, false);
        mapScreen.gameObject.SetActive(true);

        object selected = null;
        Action<MapDef> capture = mapDef => selected = mapDef;
        dropdown.OnMapSelected += capture;
        try
        {
            dropdown.LoadPreferredMap(locationId);
        }
        finally
        {
            dropdown.OnMapSelected -= capture;
            mapScreen.gameObject.SetActive(wasActive);
            screen.SetParent(parent, false);
        }

        var map = selected as MapDef;
        return map != null && dropdown.GetMapDefs().Contains(map) ? new DynamicMapsPack(map) : null;
    }

    public string DescribeWhere(Vector3 mapPosition)
    {
        var def = Def;
        var labels = new List<(string Text, Vector3 Position)>();
        foreach (var label in def.Labels ?? [])
            labels.Add((label.Text, label.Position));
        return MapWhere.Describe(def.Bounds.Min, def.Bounds.Max, def.CoordinateRotation, labels, mapPosition);
    }

    public GameObject CreateOverview(GameObject parent)
    {
        var def = Def;
        var map = UiHelpers.CreateUIGameObject(parent, "InfilMap");
        map.AddComponent<Canvas>();
        map.AddComponent<GraphicRaycaster>();
        var rect = map.GetRectTransform();
        rect.sizeDelta = MapMath.GetRotatedRectangle(def.Bounds.Max - def.Bounds.Min, def.CoordinateRotation);
        rect.localRotation = Quaternion.Euler(0f, 0f, def.CoordinateRotation);

        var layerContainer = UiHelpers.CreateUIGameObject(map, "Layers");
        foreach (var pair in def.Layers)
        {
            var layer = MapLayer.Create(layerContainer, pair.Key, pair.Value, -def.CoordinateRotation);
            layer.IsOnDefaultLevel = layer.Level == def.DefaultLevel;
        }

        var labelContainer = UiHelpers.CreateUIGameObject(map, "Labels");
        foreach (var labelDef in def.Labels ?? [])
        {
            var label = MapLabel.Create(labelContainer, labelDef, -def.CoordinateRotation, 1f);
            label.HandleNewLayerStatus(LayerStatus.OnTop);
        }

        return map;
    }

    public void ScaleLabels(GameObject overview, float zoom)
    {
        foreach (var label in overview.GetComponentsInChildren<MapLabel>(true))
            label.GetRectTransform().localScale = Vector3.one / zoom;
    }

    public IMapFloor[] GetFloors(GameObject overview)
    {
        var layers = overview.GetComponentsInChildren<MapLayer>(true);
        var floors = new IMapFloor[layers.Length];
        for (var i = 0; i < layers.Length; i++)
            floors[i] = new Floor(layers[i]);
        return floors;
    }

    private sealed class Floor : IMapFloor
    {
        private readonly object _layer;

        public Floor(object layer) => _layer = layer;

        private MapLayer Layer => (MapLayer)_layer;

        public string Name => Layer.name;
        public int Level => Layer.Level;
        public bool IsOnDefaultLevel => Layer.IsOnDefaultLevel;
        public GameObject GameObject => Layer.gameObject;
        public Transform Transform => Layer.transform;
        public Graphic Graphic => Layer.GetComponent<Graphic>();
        public bool Contains(Vector3 mapPosition) => Layer.IsCoordinateInLayer(mapPosition);
        public float BoundVolume(Vector3 mapPosition) => Layer.GetMatchingBoundVolume(mapPosition);
    }
}
