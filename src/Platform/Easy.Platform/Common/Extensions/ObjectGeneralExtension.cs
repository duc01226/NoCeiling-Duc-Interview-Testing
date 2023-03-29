using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class ObjectGeneralExtension
{
    public static bool IsValuesDifferent(this object obj1, object obj2)
    {
        if (obj1 == null && obj2 != null)
            return true;
        if (obj2 == null && obj1 != null)
            return true;
        if (obj1 != null)
            return JsonSerializer.Serialize(obj1) != JsonSerializer.Serialize(obj2);

        return false;
    }

    public static bool IsValuesEqual(this object obj1, object obj2)
    {
        return !IsValuesDifferent(obj1, obj2);
    }

    public static T As<T>(this object obj) where T : class
    {
        return obj as T;
    }

    public static T Cast<T>(this object obj)
    {
        return (T)obj;
    }

    public static T[] BoxedInArray<T>(this T obj)
    {
        return new[]
        {
            obj
        };
    }

    public static List<T> BoxedInList<T>(this T obj)
    {
        return Util.ListBuilder.New(obj);
    }

    public static string ToJson<T>(this T obj)
    {
        return PlatformJsonSerializer.Serialize(obj);
    }

    public static string ToFormattedJson<T>(this T obj)
    {
        return PlatformJsonSerializer.Serialize(obj, PlatformJsonSerializer.CurrentOptions.Value.Clone().With(_ => _.WriteIndented = true));
    }

    public static string ToJson<T>(this T obj, JsonSerializerOptions options)
    {
        return PlatformJsonSerializer.Serialize(obj, options);
    }

    public static string GetContentHash<T>(this T obj)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(obj.ToJson())));
    }

    public static object GetPropValue(this object source, string propName)
    {
        return source.GetType()
            .GetProperty(propName)
            ?.GetValue(source, null);
    }

    public static T GetPropValue<T>(this object source, string propName)
    {
        return GetPropValue(source, propName).Cast<T>();
    }
}
