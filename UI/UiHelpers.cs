using TMPro;
using UnityEngine;

namespace Liquidwarp.Infil.UI;

public static class UiHelpers
{
    private static readonly Vector2 Center = new(0.5f, 0.5f);

    public static void ResetRectTransform(this GameObject gameObject)
    {
        var rect = gameObject.transform as RectTransform;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector2.zero;
        rect.anchoredPosition3D = Vector3.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchorMin = Center;
        rect.anchorMax = Center;
        rect.pivot = Center;
    }

    public static RectTransform GetRectTransform(this GameObject gameObject) =>
        gameObject.transform as RectTransform;

    public static RectTransform GetRectTransform(this Component component) =>
        component.transform as RectTransform;

    public static GameObject CreateUIGameObject(GameObject parent, string name)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.layer;
        child.transform.SetParent(parent.transform);
        child.ResetRectTransform();
        return child;
    }

    public static bool TrySetTMPOutline(TextMeshProUGUI text)
    {
        if (text == null)
            return false;

        try
        {
            text.outlineColor = new Color32(0, 0, 0, 255);
            text.outlineWidth = 0.15f;
            text.fontStyle = FontStyles.Bold;
            text.ForceMeshUpdate(true, true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
