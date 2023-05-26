using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Easy.Platform.Common.JsonSerialization;

public sealed class PlatformPrivateConstructorContractResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object && jsonTypeInfo.CreateObject is null &&
            jsonTypeInfo.Type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0)
            // The type doesn't have public constructors
            jsonTypeInfo.CreateObject = () => Activator.CreateInstance(jsonTypeInfo.Type, true);

        return jsonTypeInfo;
    }
}
