using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace Liquidwarp.Infil.Compat;

public static class MapVariantsMaps
{
    public const string Guid = "com.lennoxp90.mapvariants";

    private const string ApiType = "MapVariants.Client.Api.Maps";
    private const string Backport = "backport";
    private const string Unmanaged = "unmanaged";

    private static bool _apiLoaded;
    private static Func<string, bool> _clientManages;
    private static Func<string> _currentLocationId;
    private static Func<string> _currentVariant;
    private static Func<bool> _isBackport;
    private static Task<HashSet<string>> _serverManages;

    private static readonly bool Loaded = Chainloader.PluginInfos.ContainsKey(Guid);

    public static void Preload()
    {
        if (Loaded)
            _serverManages = LoadServerList();
    }

    public static bool Manages(string locationId)
    {
        if (locationId == null || !Loaded)
            return false;

        LoadApi();
        if (_clientManages != null && !_clientManages(locationId))
            return false;

        var serverManages = _serverManages?.Status == TaskStatus.RanToCompletion ? _serverManages.Result : null;
        return serverManages?.Contains(locationId) ?? _clientManages != null;
    }

    public static bool Prompts(string locationId)
    {
        if (locationId == null || !Loaded)
            return false;

        LoadApi();
        return _clientManages?.Invoke(locationId) == true;
    }

    public static bool? ChoseBackport(string locationId)
    {
        LoadApi();
        var decided = _currentLocationId != null && _currentVariant != null && _isBackport != null
            && string.Equals(_currentLocationId(), locationId, StringComparison.OrdinalIgnoreCase)
            && _currentVariant() != null;

        return decided ? _isBackport() : null;
    }

    public static async Task<bool?> ServesBackport(string locationId)
    {
        var json = await RequestHandler.GetJsonAsync($"/mapvariants/hostvariant?locationId={Uri.EscapeDataString(locationId)}");
        if (string.IsNullOrEmpty(json))
            return null;

        var variant = ((string)JObject.Parse(json)["variant"])?.Trim();
        if (string.IsNullOrEmpty(variant) || string.Equals(variant, Unmanaged, StringComparison.OrdinalIgnoreCase))
            return null;

        return string.Equals(variant, Backport, StringComparison.OrdinalIgnoreCase);
    }

    private static void LoadApi()
    {
        if (_apiLoaded)
            return;

        _apiLoaded = true;
        try
        {
            var api = AccessTools.TypeByName(ApiType);
            if (api == null)
            {
                Plugin.LogSource.LogWarning($"MapVariants has no {ApiType}, relying on the server's list alone");
                return;
            }

            var isManaged = AccessTools.Method(api, "IsManaged", [typeof(string)]);
            if (isManaged != null)
                _clientManages = AccessTools.MethodDelegate<Func<string, bool>>(isManaged);
            else
                Plugin.LogSource.LogWarning("MapVariants has no Maps.IsManaged, relying on the server's list alone");

            _currentLocationId = Getter<string>(api, "CurrentLocationId");
            _currentVariant = Getter<string>(api, "CurrentVariant");
            _isBackport = Getter<bool>(api, "IsBackport");
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogWarning($"Could not read MapVariants' API: {ex.Message}");
        }
    }

    private static Func<T> Getter<T>(Type api, string property)
    {
        var getter = AccessTools.PropertyGetter(api, property);
        if (getter != null)
            return AccessTools.MethodDelegate<Func<T>>(getter);

        Plugin.LogSource.LogWarning($"MapVariants has no Maps.{property}, its maps will use the default spawns");
        return null;
    }

    private static async Task<HashSet<string>> LoadServerList()
    {
        try
        {
            var json = await RequestHandler.GetJsonAsync("/mapvariants/managed");
            var ids = JObject.Parse(json)["locationIds"]?.ToObject<string[]>() ?? [];
            return new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Plugin.LogSource.LogWarning($"Could not ask MapVariants which maps it manages, going by its client alone: {ex.Message}");
            return null;
        }
    }
}
