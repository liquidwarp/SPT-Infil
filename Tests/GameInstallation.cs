using System.Reflection;

namespace Liquidwarp.Infil.Tests;

internal static class GameInstallation
{
    public static string Root { get; } = typeof(GameInstallation).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "SPTPath").Value;
}
