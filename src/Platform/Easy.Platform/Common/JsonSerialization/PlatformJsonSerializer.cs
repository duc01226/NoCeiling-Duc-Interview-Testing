using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.JsonSerialization.Converters;

namespace Easy.Platform.Common.JsonSerialization;

/// <summary>
/// Provides utility methods for JSON serialization and deserialization with customizable options.
/// </summary>
public static class PlatformJsonSerializer
{
    /// <summary>
    /// Gets the default JSON serialization options.
    /// </summary>
    public static readonly JsonSerializerOptions DefaultOptions = BuildDefaultOptions();

    /// <summary>
    /// Lazy-initialized current JSON serialization options for thread safety.
    /// </summary>
    public static Lazy<JsonSerializerOptions> CurrentOptions { get; private set; } = new(() => DefaultOptions);

    /// <summary>
    /// Sets the current JSON serialization options.
    /// </summary>
    /// <param name="serializerOptions">The custom JSON serialization options.</param>
    public static void SetCurrentOptions(JsonSerializerOptions serializerOptions)
    {
        CurrentOptions = new Lazy<JsonSerializerOptions>(() => serializerOptions);
    }

    /// <summary>
    /// Configures the provided JSON serialization options with platform-specific best practices and customizations.
    /// </summary>
    /// <param name="options">The JSON serialization options to configure.</param>
    /// <param name="useJsonStringEnumConverter">Whether to use the <see cref="JsonStringEnumConverter" />.</param>
    /// <param name="useCamelCaseNaming">Whether to use camel case property naming.</param>
    /// <param name="customConverters">Additional custom JSON converters.</param>
    /// <param name="ignoreJsonConverterTypes">Input list of default platform json converters that you want to be ignored</param>
    /// <returns>The configured JSON serialization options.</returns>
    public static JsonSerializerOptions ConfigOptions(
        JsonSerializerOptions options,
        bool useJsonStringEnumConverter = true,
        bool useCamelCaseNaming = false,
        List<JsonConverter> customConverters = null,
        HashSet<Type> ignoreJsonConverterTypes = null)
    {
        options.TypeInfoResolver = new PlatformJsonTypeInfoResolver();
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.ReadCommentHandling = JsonCommentHandling.Skip;
        options.PropertyNameCaseInsensitive = true;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;

        if (useCamelCaseNaming)
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        /*
         * the order of converters in the Converters list in JsonSerializerOptions can affect how serialization and deserialization are handled in .NET.
         * When serializing or deserializing objects, the System.Text.Json library processes the converters in the order they are added to the Converters list.
         * The first converter that can handle the type being serialized or deserialized will be used.
         */
        options.Converters.Clear();

        if (useJsonStringEnumConverter)
            options.Converters.Add(new JsonStringEnumConverter());

        if (ignoreJsonConverterTypes?.Contains(typeof(PlatformObjectJsonConverter)) != true)
            options.Converters.Add(new PlatformObjectJsonConverter());
        if (ignoreJsonConverterTypes?.Contains(typeof(PlatformClassTypeJsonConverter)) != true)
            options.Converters.Add(new PlatformClassTypeJsonConverter());
        if (ignoreJsonConverterTypes?.Contains(typeof(PlatformIgnoreMethodBaseJsonConverter)) != true)
            options.Converters.Add(new PlatformIgnoreMethodBaseJsonConverter());
        if (ignoreJsonConverterTypes?.Contains(typeof(PlatformDateTimeJsonConverter)) != true)
            options.Converters.Add(new PlatformDateTimeJsonConverter());
        if (ignoreJsonConverterTypes?.Contains(typeof(PlatformNullableDateTimeJsonConverter)) != true)
            options.Converters.Add(new PlatformNullableDateTimeJsonConverter());
        if (ignoreJsonConverterTypes?.Contains(typeof(PlatformDateOnlyJsonConverter)) != true)
            options.Converters.Add(new PlatformDateOnlyJsonConverter());
        if (ignoreJsonConverterTypes?.Contains(typeof(PlatformNullableDateOnlyJsonConverter)) != true)
            options.Converters.Add(new PlatformNullableDateOnlyJsonConverter());
        if (ignoreJsonConverterTypes?.Contains(typeof(PlatformPrimitiveTypeToStringJsonConverter)) != true)
            options.Converters.Add(new PlatformPrimitiveTypeToStringJsonConverter());

        customConverters?.ForEach(options.Converters.Add);

        return options;
    }

    /// <summary>
    /// Builds the default JSON serialization options.
    /// </summary>
    /// <param name="useJsonStringEnumConverter">Whether to use the <see cref="JsonStringEnumConverter" />.</param>
    /// <param name="useCamelCaseNaming">Whether to use camel case property naming.</param>
    /// <param name="customConverters">Additional custom JSON converters.</param>
    /// <returns>The default JSON serialization options.</returns>
    public static JsonSerializerOptions BuildDefaultOptions(
        bool useJsonStringEnumConverter = true,
        bool useCamelCaseNaming = false,
        List<JsonConverter> customConverters = null)
    {
        return ConfigOptions(new JsonSerializerOptions(), useJsonStringEnumConverter, useCamelCaseNaming, customConverters);
    }

    /// <summary>
    /// Serializes the specified value to a JSON string using the provided options or the current default options.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="forceUseRuntimeType">Whether to force the use of the runtime type for abstract types.</param>
    /// <returns>The JSON string representation of the serialized value.</returns>
    public static string Serialize<TValue>(TValue value, bool forceUseRuntimeType)
    {
        return Serialize(value, customSerializerOptions: null, forceUseRuntimeType);
    }

    /// <summary>
    /// Serializes the specified value to a JSON string using the provided options or the current default options.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The JSON string representation of the serialized value.</returns>
    public static string Serialize<TValue>(TValue value)
    {
        return Serialize(value, customSerializerOptions: null);
    }

    /// <summary>
    /// Serializes the specified value to a JSON string using the provided options or the current default options.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="customSerializerOptions">Custom JSON serialization options.</param>
    /// <param name="forceUseRuntimeType">Whether to force the use of the runtime type for abstract types.</param>
    /// <returns>The JSON string representation of the serialized value.</returns>
    public static string Serialize<TValue>(TValue value, JsonSerializerOptions customSerializerOptions, bool forceUseRuntimeType = false)
    {
        if (typeof(TValue).IsAbstract || forceUseRuntimeType)
            try
            {
                // Try to use the real runtime type to support TValue as an abstract base type.
                // Serialize exactly the type. If not successful, fallback to the original type.
                return JsonSerializer.Serialize(value, value.GetType(), customSerializerOptions ?? CurrentOptions.Value);
            }
            catch
            {
                return JsonSerializer.Serialize(value, typeof(TValue), customSerializerOptions ?? CurrentOptions.Value);
            }

        return JsonSerializer.Serialize(value, typeof(TValue), customSerializerOptions ?? CurrentOptions.Value);
    }

    public static string Serialize<TValue>(TValue value, Action<JsonSerializerOptions> customSerializerOptionsConfig)
    {
        return Serialize(value, customSerializerOptions: CurrentOptions.Value.Clone().With(customSerializerOptionsConfig));
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
        JsonSerializerOptions customSerializerOptions = null,
        bool forceUseRuntimeType = false)
    {
        if (typeof(TValue).IsAbstract || forceUseRuntimeType)
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

        return JsonSerializer.SerializeToUtf8Bytes(value, typeof(TValue), customSerializerOptions ?? CurrentOptions.Value);
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

    public static T TryDeserializeOrDefault<T>(string jsonValue, T defaultValue = default)
    {
        try
        {
            return Deserialize<T>(jsonValue);
        }
        catch
        {
            return defaultValue;
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
