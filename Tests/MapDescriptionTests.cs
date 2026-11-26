using DynamicMaps.Data;
using Liquidwarp.Infil.Map;
using NUnit.Framework;
using UnityEngine;

namespace Liquidwarp.Infil.Tests;

[TestFixture]
public class MapDescriptionTests
{
    [Test]
    public void MissingDynamicMapsInstanceYieldsNoOverview()
    {
        // Dynamic Maps opens a native installation-error dialog if its SVG parser cannot load.
        Assert.That(Type.GetType("Unity.VectorGraphics.SVGParser, Unity.VectorGraphics"), Is.Not.Null);
        Assert.That(MapDefCatalog.Find("woods"), Is.Null);
    }

    [Test]
    public void UsesNearestNamedLandmarkInTheMapPlaneAndFormatsMultilineNames()
    {
        var map = new MapDef
        {
            Labels =
            [
                new MapLabelDef { Text = " ", Position = new Vector3(60, 12, 0) },
                new MapLabelDef { Text = "Warehouse", Position = new Vector3(85, 12, 0) },
                new MapLabelDef { Text = "  Train\nStation ", Position = new Vector3(61, 12, 500) }
            ]
        };

        Assert.That(MapDefCatalog.DescribeWhere(map, new Vector3(60, 12, 0)), Is.EqualTo("near Train Station"));
    }

    [TestCase(0, 0, 0, "middle")]
    [TestCase(0.32f, 0, 0, "middle")]
    [TestCase(0.34f, 0, 0, "east")]
    [TestCase(1, 1, 0, "north-east")]
    [TestCase(0, 1, 0, "north")]
    [TestCase(-1, 1, 0, "north-west")]
    [TestCase(-1, 0, 0, "west")]
    [TestCase(-1, -1, 0, "south-west")]
    [TestCase(0, -1, 0, "south")]
    [TestCase(1, -1, 0, "south-east")]
    [TestCase(1, 0, 90, "north")]
    [TestCase(1, 0, -90, "south")]
    public void CompassUsesTheDisplayedMapCenterScaleAndRotation(float x, float y, float rotation, string direction)
    {
        var map = new MapDef
        {
            Bounds = new BoundingRectangle { Min = new Vector2(-120, 20), Max = new Vector2(280, 100) },
            CoordinateRotation = rotation,
            Labels = null
        };
        var position = new Vector3(80f + x * 200f, 60f + y * 40f, 0f);

        Assert.That(MapDefCatalog.DescribeWhere(map, position), Is.EqualTo($"in the {direction}"));
    }

    [Test]
    public void NoLandmarksAndMissingOrZeroSizedBoundsYieldNoDescription()
    {
        var map = new MapDef { Labels = [new MapLabelDef { Text = "\t" }] };
        Assert.That(MapDefCatalog.DescribeWhere(map, Vector3.zero), Is.Null);

        map.Bounds = new BoundingRectangle { Min = Vector2.zero, Max = new Vector2(0, 10) };
        Assert.That(MapDefCatalog.DescribeWhere(map, Vector3.zero), Is.Null);

        map.Bounds.Max = new Vector2(10, 0);
        Assert.That(MapDefCatalog.DescribeWhere(map, Vector3.zero), Is.Null);
    }
}
