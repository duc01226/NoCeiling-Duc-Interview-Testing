using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Easy.Platform.Common.JsonSerialization.Converters;

/// <summary>
/// Fix exception when serialize class with have MethodBase type is not supported
/// </summary>
public class PlatformIgnoreMethodBaseJsonConverter : JsonConverter<MethodBase>
{
    public override MethodBase Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return null;
    }

    public override void Write(
        Utf8JsonWriter writer,
        MethodBase value,
        JsonSerializerOptions options
    )
    {
        writer.WriteNullValue();
    }
}
