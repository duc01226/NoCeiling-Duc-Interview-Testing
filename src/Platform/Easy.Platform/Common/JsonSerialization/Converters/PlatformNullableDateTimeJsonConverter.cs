using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.JsonSerialization.Converters;

public class PlatformNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = reader.TokenType;

        if (type == JsonTokenType.Null) return null;

        var strValue = reader.GetString();
        if (strValue.IsNullOrEmpty()) return null;

        var parsedResult = DateTime.TryParse(strValue, out var parsedDate);

        return parsedDate;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}
