using System.Reflection;
using BepInEx;
using NUnit.Framework;
using static BepInEx.BepInDependency;

namespace Liquidwarp.Infil.Tests;

public sealed class PluginDependencyTests
{
    [TestCase("com.SPT.custom", DependencyFlags.HardDependency)]
    [TestCase("com.mpstark.dynamicmaps", DependencyFlags.HardDependency)]
    [TestCase("com.fika.core", DependencyFlags.SoftDependency)]
    [TestCase("com.lennoxp90.mapvariants", DependencyFlags.SoftDependency)]
    public void BepInExLoadsThePluginWithItsSupportedDependencies(string guid, DependencyFlags expected)
    {
        var dependency = typeof(Plugin).GetCustomAttributes<BepInDependency>().Single(item => item.DependencyGUID == guid);
        Assert.That(dependency.Flags, Is.EqualTo(expected));
    }
}
