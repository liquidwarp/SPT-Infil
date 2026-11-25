using UnityEngine;

namespace Liquidwarp.Infil.Map;

public static class MapMath
{
    public static Vector2 GetRotatedVector2(Vector2 vector, float degreeRotation)
    {
        var radians = degreeRotation * Mathf.Deg2Rad;
        var cos = Mathf.Cos(radians);
        var sin = Mathf.Sin(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    public static Vector2 GetRotatedRectangle(Vector2 rectangle, float degreeRotation)
    {
        var radians = degreeRotation * Mathf.Deg2Rad;
        var absCos = Mathf.Abs(Mathf.Cos(radians));
        var absSin = Mathf.Abs(Mathf.Sin(radians));
        return new Vector2(
            rectangle.x * absCos + rectangle.y * absSin,
            rectangle.x * absSin + rectangle.y * absCos);
    }

    public static Vector2 GetMidpoint(Vector2 minBound, Vector2 maxBound) => (minBound + maxBound) * 0.5f;

    public static Vector3 ConvertToMapPosition(Vector3 unityPosition) =>
        new(unityPosition.x, unityPosition.z, unityPosition.y);
}
