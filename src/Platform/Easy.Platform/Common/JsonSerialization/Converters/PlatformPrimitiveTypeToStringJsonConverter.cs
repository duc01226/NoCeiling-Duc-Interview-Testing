using System.Text.Json;
using System.Text.Json.Serialization;

namespace Easy.Platform.Common.JsonSerialization.Converters;

/// <summary>
/// Support auto primitive type to string on Deserialize
/// </summary>
public class PlatformPrimitiveTypeToStringJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt32(out var intVal) => intVal.ToString(),
            JsonTokenType.Number when reader.TryGetInt64(out var longVal) => longVal.ToString(),
            JsonTokenType.Number when reader.TryGetDouble(out var doubleVal) => doubleVal.ToString(),
            JsonTokenType.True => "True",
            JsonTokenType.False => "False",
            _ => throw new JsonException() // StartObject, StartArray, Null    
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}
