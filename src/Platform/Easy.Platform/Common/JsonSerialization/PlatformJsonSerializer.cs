using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization.Converters;

namespace Easy.Platform.Common.JsonSerialization;

public static class PlatformJsonSerializer
{
    public static readonly JsonSerializerOptions DefaultOptions = BuildDefaultOptions();

    /// <summary>
    /// Use Lazy for thread safe
    /// </summary>
    public static Lazy<JsonSerializerOptions> CurrentOptions { get; private set; } = new(() => DefaultOptions);

    public static void SetCurrentOptions(JsonSerializerOptions serializerOptions)
    {
        CurrentOptions = new Lazy<JsonSerializerOptions>(() => serializerOptions);
    }

    /// <summary>
    /// Config JsonSerializerOptions with some platform best practices options. <br />
    /// Support some customization
    /// </summary>
    public static JsonSerializerOptions ConfigOptions(
        JsonSerializerOptions options,
        bool useJsonStringEnumConverter = true,
        bool useCamelCaseNaming = false,
        List<JsonConverter> customConverters = null)
    {
        options.TypeInfoResolver = new PlatformPrivateConstructorContractResolver();
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.ReadCommentHandling = JsonCommentHandling.Skip;
        options.PropertyNameCaseInsensitive = true;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        if (useCamelCaseNaming)
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        options.Converters.Clear();
        if (useJsonStringEnumConverter)
            options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new PlatformObjectJsonConverter());
        options.Converters.Add(new PlatformClassTypeJsonConverter());
        options.Converters.Add(new PlatformIgnoreMethodBaseJsonConverter());
        options.Converters.Add(new PlatformNullableDateTimeJsonConverter());
        options.Converters.Add(new PlatformFormattedStringToDateTimeJsonConverter());
        options.Converters.Add(new PlatformPrimitiveTypeToStringJsonConverter());
        customConverters?.ForEach(options.Converters.Add);

        return options;
    }

    public static JsonSerializerOptions BuildDefaultOptions(
        bool useJsonStringEnumConverter = true,
        bool useCamelCaseNaming = false,
        List<JsonConverter> customConverters = null)
    {
        return ConfigOptions(new JsonSerializerOptions(), useJsonStringEnumConverter, useCamelCaseNaming, customConverters);
    }

    public static string Serialize<TValue>(TValue value)
    {
        try
        {
            // Try to use real runtime type to support TValue is abstract base type. Serialize exactly the type.
            // If not work come back to original type
            return JsonSerializer.Serialize(value, value.GetType(), CurrentOptions.Value);
        }
        catch (Exception)
        {
            return JsonSerializer.Serialize(value, typeof(TValue), CurrentOptions.Value);
        }
    }

    public static string Serialize<TValue>(TValue value, JsonSerializerOptions customSerializerOptions)
    {
        try
        {
            // Try to use real runtime type to support TValue is abstract base type. Serialize exactly the type.
            // If not work come back to original type
            return JsonSerializer.Serialize(value, value.GetType(), customSerializerOptions ?? CurrentOptions.Value);
        }
        catch (Exception)
        {
            return JsonSerializer.Serialize(value, typeof(TValue), customSerializerOptions ?? CurrentOptions.Value);
        }
    }

    public static string Serialize<TValue>(TValue value, Action<JsonSerializerOptions> customSerializerOptionsConfig)
    {
        try
        {
            // Try to use real runtime type to support TValue is abstract base type. Serialize exactly the type.
            // If not work come back to original type
            return JsonSerializer.Serialize(value, value.GetType(), CurrentOptions.Value.Clone().With(customSerializerOptionsConfig));
        }
        catch (Exception)
        {
            return JsonSerializer.Serialize(value, typeof(TValue), CurrentOptions.Value.Clone().With(customSerializerOptionsConfig));
        }
    }

    public static string SerializeWithDefaultOptions<TValue>(
        TValue value,
        bool useJsonStringEnumConverter = true,
        bool useCamelCaseNaming = false,
        List<JsonConverter> customConverters = null)
    {
        return Serialize(value, BuildDefaultOptions(useJsonStringEnumConverter, useCamelCaseNaming, customConverters));
    }

    public static T Deserialize<T>(string jsonValue)
    {
        return JsonSerializer.Deserialize<T>(jsonValue, CurrentOptions.Value);
    }

    public static T DeserializeWithDefaultOptions<T>(
        string jsonValue,
        bool useJsonStringEnumConverter = true,
        bool useCamelCaseNaming = false,
        List<JsonConverter> customConverters = null)
    {
        return Deserialize<T>(jsonValue, BuildDefaultOptions(useJsonStringEnumConverter, useCamelCaseNaming, customConverters));
    }

    public static T Deserialize<T>(string jsonValue, JsonSerializerOptions customSerializerOptions)
    {
        return JsonSerializer.Deserialize<T>(jsonValue, customSerializerOptions ?? CurrentOptions.Value);
    }

    public static object Deserialize(
        string jsonValue,
        Type returnType,
        JsonSerializerOptions customSerializerOptions = null)
    {
        return JsonSerializer.Deserialize(jsonValue, returnType, customSerializerOptions ?? CurrentOptions.Value);
    }

    public static byte[] SerializeToUtf8Bytes<TValue>(
        TValue value,
        JsonSerializerOptions customSerializerOptions = null)
    {
        try
        {
            // Try to use real runtime type to support TValue is abstract base type. Serialize exactly the type.
            // If not work come back to original type
            return JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), customSerializerOptions ?? CurrentOptions.Value);
        }
        catch (Exception)
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, typeof(TValue), customSerializerOptions ?? CurrentOptions.Value);
        }
    }

    public static TValue Deserialize<TValue>(
        ReadOnlySpan<byte> utf8Json,
        JsonSerializerOptions customSerializerOptions = null)
    {
        return JsonSerializer.Deserialize<TValue>(utf8Json, customSerializerOptions ?? CurrentOptions.Value);
    }

    public static object Deserialize(
        ReadOnlySpan<byte> utf8Json,
        Type returnType,
        JsonSerializerOptions customSerializerOptions = null)
    {
        return JsonSerializer.Deserialize(utf8Json, returnType, customSerializerOptions ?? CurrentOptions.Value);
    }

    public static T TryDeserializeOrDefault<T>(string jsonValue)
    {
        try
        {
            return Deserialize<T>(jsonValue);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Try to Deserialize json string.
    /// If success return true and out the deserialized value of type <see cref="T" />.
    /// If error return false and out default of type <see cref="T" />
    /// </summary>
    public static bool TryDeserialize<T>(
        string json,
        out T deserializedValue,
        JsonSerializerOptions options = null)
    {
        var tryDeserializeResult = TryDeserialize(
            json,
            typeof(T),
            out var deserializedObjectValue,
            options ?? CurrentOptions.Value);

        deserializedValue = (T)deserializedObjectValue;

        return tryDeserializeResult;
    }

    public static bool TryDeserialize(
        string json,
        Type deserializeType,
        out object deserializedValue,
        JsonSerializerOptions options = null)
    {
        try
        {
            deserializedValue = JsonSerializer.Deserialize(json, deserializeType, options ?? CurrentOptions.Value);
            return true;
        }
        catch (Exception)
        {
            deserializedValue = default;
            return false;
        }
    }
}
