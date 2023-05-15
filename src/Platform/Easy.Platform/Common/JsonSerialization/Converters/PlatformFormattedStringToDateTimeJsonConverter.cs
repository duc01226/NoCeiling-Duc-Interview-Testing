using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.JsonSerialization.Converters;

//TODO: Remove it and force client to fix not to push string date to server
public class PlatformFormattedStringToDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var strValue = reader.GetString();

        return strValue.IsNullOrEmpty() ? default : DateTime.Parse(strValue!);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}
