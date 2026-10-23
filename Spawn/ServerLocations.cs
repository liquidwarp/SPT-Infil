using System;
using System.IO;
using System.Threading.Tasks;
using EFT;
using JsonType;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace Liquidwarp.Infil.Spawn;

public static class ServerLocations
{
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(EftJsonConverters.SerializerSettings);

    public static async Task<LocationSettings.Location> Fetch(string locationId)
    {
        var json = await RequestHandler.GetJsonAsync("/client/locations");
        return string.IsNullOrEmpty(json) ? null : await Task.Run(() => Extract(json, locationId));
    }

    private static LocationSettings.Location Extract(string json, string locationId)
    {
        using var reader = new JsonTextReader(new StringReader(json));
        while (reader.Read() && !(reader.TokenType == JsonToken.StartObject && reader.Path == "data.locations"))
        {
        }

        while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
        {
            reader.Read();
            var location = JToken.ReadFrom(reader);
            if (string.Equals((string)location["Id"], locationId, StringComparison.OrdinalIgnoreCase))
                return location.ToObject<LocationSettings.Location>(Serializer);
        }

        return null;
    }
}
