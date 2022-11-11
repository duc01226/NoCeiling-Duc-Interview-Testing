using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization.Converters;

namespace Easy.Platform.Common.JsonSerialization;

public static class PlatformJsonSerializer
{
    public static readonly JsonSerializerOptions DefaultOptions;

    static PlatformJsonSerializer()
    {
        DefaultOptions = BuildDefaultOptions();
    }

    /// <summary>
    /// Use Lazy for thread safe
    /// </summary>
    public static Lazy<JsonSerializerOptions> CurrentOptions { get; private set; } =
        new(() => DefaultOptions);

    public static void SetCurrentOptions(JsonSerializerOptions serializerOptions)
    {
        CurrentOptions = new Lazy<JsonSerializerOptions>(() => serializerOptions);
    }

    public static JsonSerializerOptions ConfigOptionsByCurrentOptions(
        JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = CurrentOptions.Value.DefaultIgnoreCondition;
        options.PropertyNamingPolicy = CurrentOptions.Value.PropertyNamingPolicy;

        options.Converters.Clear();
        CurrentOptions.Value.Converters.ForEach(p => options.Converters.Add(p));

        return options;
    }

    public static JsonSerializerOptions ConfigOptions(
        JsonSerializerOptions options,
        bool useJsonStringEnumConverter = true,
        bool useCamelCaseNaming = false,
        List<JsonConverter> customConverters = null)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        if (useCamelCaseNaming)
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        options.Converters.Clear();
        if (useJsonStringEnumConverter)
            options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new PlatformObjectJsonConverter());
        options.Converters.Add(new PlatformDynamicJsonConverter());
        options.Converters.Add(new PlatformClassTypeJsonConverter());
        customConverters?.ForEach(p => options.Converters.Add(p));

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
        return JsonSerializer.Serialize(value, value?.GetType() ?? typeof(TValue), CurrentOptions.Value);
    }

    public static string Serialize<TValue>(TValue value, JsonSerializerOptions customSerializerOptions)
    {
        return JsonSerializer.Serialize(
            value,
            value?.GetType() ?? typeof(TValue),
            customSerializerOptions ?? CurrentOptions.Value);
    }

    public static T Deserialize<T>(string jsonValue)
    {
        return JsonSerializer.Deserialize<T>(jsonValue, CurrentOptions.Value);
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
        return JsonSerializer.SerializeToUtf8Bytes(
            value,
            value?.GetType() ?? typeof(TValue),
            customSerializerOptions ?? CurrentOptions.Value);
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
