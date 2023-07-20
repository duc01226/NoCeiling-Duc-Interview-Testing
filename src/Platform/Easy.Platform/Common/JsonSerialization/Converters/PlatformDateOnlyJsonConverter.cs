using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization.Converters.Helpers;

namespace Easy.Platform.Common.JsonSerialization.Converters;

public class PlatformDateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = reader.TokenType;

        if (type == JsonTokenType.Null) return default;

        var strValue = reader.GetString();
        if (strValue.IsNullOrEmpty()) return default;

        return PlatformStringToDateTimeConverterHelper.TryReadDateOnly(strValue) ?? throw new Exception($"Could not parse {strValue} to DateOnly");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}

public class PlatformNullableDateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = reader.TokenType;

        if (type == JsonTokenType.Null) return null;

        var strValue = reader.GetString();

        return PlatformStringToDateTimeConverterHelper.TryReadDateOnly(strValue);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}
