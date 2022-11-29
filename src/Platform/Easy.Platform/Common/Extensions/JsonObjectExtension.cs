using System.Text.Json;
using System.Text.Json.Nodes;

namespace Easy.Platform.Common.Extensions;

public static class JsonObjectExtension
{
    public static JsonObject ToJsonObject(this Dictionary<string, object> values)
    {
        return values.Aggregate(
            seed: new JsonObject(),
            (jsonObj, keyValue) =>
            {
                jsonObj.Add(keyValue.Key, (JsonNode)keyValue.Value);
                return jsonObj;
            });
    }

    public static JsonObject DeepClone(this JsonObject jsonObject)
    {
        return (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(jsonObject));
    }
}
