using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;

namespace Easy.Platform.EfCore.JsonSerialization;

/// <summary>
/// Ignore LazyLoader prop to fix json serialize issues for lazy loading proxy entity
/// </summary>
public class PlatformILazyLoadingJsonConverter : JsonConverter<ILazyLoader>
{
    public override ILazyLoader Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ILazyLoader value, JsonSerializerOptions options)
    {
        writer.WriteNullValue();
    }
}

/// <summary>
/// Ignore LazyLoader prop to fix json serialize issues for lazy loading proxy entity
/// </summary>
public class PlatformLazyLoadingJsonConverter : JsonConverter<LazyLoader>
{
    public override LazyLoader Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, LazyLoader value, JsonSerializerOptions options)
    {
        writer.WriteNullValue();
    }
}
