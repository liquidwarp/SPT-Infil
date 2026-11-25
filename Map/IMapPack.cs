using UnityEngine;
using UnityEngine.UI;

namespace Liquidwarp.Infil.Map;

public interface IMapPack
{
    string SourceName { get; }
    float CoordinateRotation { get; }
    Vector2 BoundsMin { get; }
    Vector2 BoundsMax { get; }

    string DescribeWhere(Vector3 mapPosition);
    GameObject CreateOverview(GameObject parent);
    void ScaleLabels(GameObject overview, float zoom);
    IMapFloor[] GetFloors(GameObject overview);
}

public interface IMapFloor
{
    string Name { get; }
    int Level { get; }
    bool IsOnDefaultLevel { get; }
    GameObject GameObject { get; }
    Transform Transform { get; }
    Graphic Graphic { get; }
    bool Contains(Vector3 mapPosition);
    float BoundVolume(Vector3 mapPosition);
}
