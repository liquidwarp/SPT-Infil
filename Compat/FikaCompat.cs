using BepInEx.Bootstrap;

namespace Liquidwarp.Infil.Compat;

public static class FikaCompat
{
    public const string Guid = "com.fika.core";
    public const string HeadlessGuid = "com.fika.headless";

    public static readonly bool Loaded = Chainloader.PluginInfos.ContainsKey(Guid);

    public static readonly bool Headless = Chainloader.PluginInfos.ContainsKey(HeadlessGuid);
}
