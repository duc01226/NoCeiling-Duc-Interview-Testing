using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.JsonSerialization.Converters;

// TODO: Dotnet core 8 api return IAsyncEnumerable => resolve type is object which cause error when serialized. Because SystemTextJsonOutputFormatter => (declaredTypeJsonInfo.ShouldUseWith(runtimeType)) is false. Following issues: https://github.com/dotnet/aspnetcore/issues/54374
/// <summary>
/// This is used to fix deserialize object add ValueKind to value
/// https://github.com/dotnet/runtime/issues/31408
/// </summary>
public class PlatformObjectJsonConverter : JsonConverter<object>
{
    /// <summary>
    /// This help to fix issues. Using <see cref="JsonSerializer" />
    /// to Deserialize to Object/dynamic will lead to the object is a JsonElement
    /// and is wrapped object with only on property ValueKind.
    /// Also if the actual value object is number/string etc... it will not work when try to
    /// Deserialize, the actual object is still a JsonElement object.
    /// This will fix the issues.
    /// References: https://github.com/dotnet/runtime/issues/31408
    /// </summary>
    public static dynamic TryGetReflectionDynamicIfJsonElement(dynamic dynamicObject)
    {
        if (dynamicObject is JsonElement jsonElement)
            return TryGetReflectionDynamic(jsonElement);

        return dynamicObject;
    }

    /// <inheritdoc cref="TryGetReflectionDynamicIfJsonElement" />
    public static dynamic TryGetReflectionDynamic(JsonElement dynamicObjectAsJsonElement)
    {
        switch (dynamicObjectAsJsonElement.ValueKind)
        {
            case JsonValueKind.Number:
            {
                if (dynamicObjectAsJsonElement.TryGetInt32(out var valInt32))
                    return valInt32;
                if (dynamicObjectAsJsonElement.TryGetInt64(out var valInt64))
                    return valInt64;
                if (dynamicObjectAsJsonElement.TryGetDouble(out var valDouble))
                    return valDouble;

                break;
            }

            case JsonValueKind.False:
            case JsonValueKind.True:
                return dynamicObjectAsJsonElement.GetBoolean();

            case JsonValueKind.String:
            {
                try
                {
                    if (dynamicObjectAsJsonElement.TryGetDateTimeOffset(out var dateTimeOffsetValue))
                        return dateTimeOffsetValue;
                    if (dynamicObjectAsJsonElement.TryGetDateTime(out var dateValue))
                        return dateValue;
                    return dynamicObjectAsJsonElement.GetString();
                }
                catch
                {
                    return dynamicObjectAsJsonElement.GetString();
                }
            }

            case JsonValueKind.Array:
                return dynamicObjectAsJsonElement
                    .EnumerateArray()
                    .Select(o => TryGetReflectionDynamicIfJsonElement(o))
                    .ToArray();

            case JsonValueKind.Object:
            {
                var keyValueObject = new Dictionary<string, object>();

                var dynamicObjectPropEnumerator = dynamicObjectAsJsonElement.EnumerateObject();
                while (dynamicObjectPropEnumerator.MoveNext())
                {
                    keyValueObject.Add(
                        dynamicObjectPropEnumerator.Current.Name,
                        TryGetReflectionDynamic(dynamicObjectPropEnumerator.Current.Value));
                }

                return keyValueObject;
            }
        }

        return null;
    }

    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = reader.TokenType;

        if (type == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var valInt32))
                return valInt32;

            if (reader.TryGetInt64(out var valInt64))
                return valInt64;

            if (reader.TryGetDouble(out var valDouble))
                return valDouble;
        }

        if (type == JsonTokenType.String)
            return reader.GetString();

        if (type is JsonTokenType.True or JsonTokenType.False)
            return reader.GetBoolean();

        // copied from built-in JsonConverterObject in System.Text.Json.Serialization.Converters
        using (var document = JsonDocument.ParseValue(ref reader))
        {
            var parsedObjectAsJsonElement = document.RootElement.Clone();
            return TryGetReflectionDynamicIfJsonElement(parsedObjectAsJsonElement);
        }
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        // We do not handle write object, so we remove itself to prevent stack overflow
        var removedItSelfOptions = options.Clone()
            .With(options => options.Converters.RemoveWhere(p => p is PlatformObjectJsonConverter, out _));

        // Let the default.NET behavior handle write object
        JsonSerializer.Serialize(writer, value, value.GetType(), removedItSelfOptions);
    }
}
