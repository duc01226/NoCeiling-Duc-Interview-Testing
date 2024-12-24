using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Easy.Platform.Common.JsonSerialization;

/// <summary>
/// Custom JSON type information resolver for platform-specific JSON serialization.
/// </summary>
public class PlatformJsonTypeInfoResolver : DefaultJsonTypeInfoResolver
{
    private static readonly ConcurrentDictionary<string, bool> IsTypeNoPublicConstructorCache = new();
    private static readonly ConcurrentDictionary<string, Func<object>> CreateObjectForeNoPublicConstructorCache = new();

    /// <summary>
    /// Gets the JSON type information for the specified type and JSON serialization options.
    /// </summary>
    /// <param name="type">The type for which to retrieve JSON type information.</param>
    /// <param name="options">The JSON serialization options.</param>
    /// <returns>The JSON type information.</returns>
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);
        var jsonTypeInfoType = jsonTypeInfo.Type;
        var jsonTypeInfoTypeName = jsonTypeInfo.Type.FullName ?? jsonTypeInfo.Type.Name;

        // If the type is an object, it has no public constructors, and the CreateObject delegate is not set
        if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object &&
            jsonTypeInfo.CreateObject is null &&
            IsTypeNoPublicConstructorCache.GetOrAdd(
                jsonTypeInfoTypeName,
                p => !jsonTypeInfoType.IsInterface && jsonTypeInfoType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0))
        {
            // Set the CreateObject delegate to use the private parameterless constructor
            jsonTypeInfo.CreateObject = CreateObjectForeNoPublicConstructorCache.GetOrAdd(
                jsonTypeInfoTypeName,
                p => () => Activator.CreateInstance(jsonTypeInfoType, nonPublic: true));
        }

        return jsonTypeInfo;
    }
}
