using BepInEx.Bootstrap;

namespace Liquidwarp.Infil.Compat;

public static class FikaCompat
{
    public const string Guid = "com.fika.core";

    public static readonly bool Loaded = Chainloader.PluginInfos.ContainsKey(Guid);
}
