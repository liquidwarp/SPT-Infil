using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EFT;
using JsonType;
using Newtonsoft.Json;
using SPT.Common.Http;

namespace Liquidwarp.Infil.Spawn;

public static class ServerLocations
{
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(EftJsonConverters.SerializerSettings);

    private static Dictionary<string, LocationSettings.Location> _locations;

    public static async Task<LocationSettings.Location> Fetch(string locationKey)
    {
        _locations ??= await Load();
        return _locations != null && _locations.TryGetValue(locationKey, out var location) ? location : null;
    }

    private static async Task<Dictionary<string, LocationSettings.Location>> Load()
    {
        var json = await RequestHandler.GetJsonAsync("/client/locations");
        return string.IsNullOrEmpty(json) ? null : await Task.Run(() => Extract(json));
    }

    private static Dictionary<string, LocationSettings.Location> Extract(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json));
        while (reader.Read() && !(reader.TokenType == JsonToken.StartObject && reader.Path == "data.locations"))
        {
        }

        return Serializer.Deserialize<Dictionary<string, LocationSettings.Location>>(reader);
    }
}
