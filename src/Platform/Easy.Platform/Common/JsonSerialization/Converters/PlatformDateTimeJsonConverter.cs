using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization.Converters.Helpers;

namespace Easy.Platform.Common.JsonSerialization.Converters;

public sealed class PlatformDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = reader.TokenType;

        if (type == JsonTokenType.Null) return default;

        var strValue = reader.GetString();
        if (strValue.IsNullOrEmpty()) return default;

        return PlatformStringToDateTimeConverterHelper.TryRead(strValue) ?? default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}
