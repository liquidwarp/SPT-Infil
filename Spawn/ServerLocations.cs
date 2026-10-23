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

    public static async Task<LocationSettings.Location> Fetch(string locationKey)
    {
        var json = await RequestHandler.GetJsonAsync("/client/locations");
        return string.IsNullOrEmpty(json) ? null : await Task.Run(() => Extract(json, locationKey));
    }

    private static LocationSettings.Location Extract(string json, string locationKey)
    {
        using var reader = new JsonTextReader(new StringReader(json));
        while (reader.Read() && !(reader.TokenType == JsonToken.StartObject && reader.Path == "data.locations"))
        {
        }

        while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
        {
            if ((string)reader.Value != locationKey)
            {
                reader.Skip();
                continue;
            }

            reader.Read();
            return Serializer.Deserialize<LocationSettings.Location>(reader);
        }

        return null;
    }
}
