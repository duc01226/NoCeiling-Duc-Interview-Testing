using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class ObjectGeneralExtension
{
    public static bool IsValuesDifferent<T1, T2>(this T1 obj1, T2 obj2)
    {
        if (obj1 == null && obj2 != null)
            return true;
        if (obj2 == null && obj1 != null)
            return true;
        if (obj1 != null)
            return PlatformJsonSerializer.Serialize(obj1) != PlatformJsonSerializer.Serialize(obj2);

        return false;
    }

    public static bool IsValuesEqual<T1, T2>(this T1 obj1, T2 obj2)
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

    public static string ToJson<T>(this T obj, bool forceUseRuntimeType = false)
    {
        return PlatformJsonSerializer.Serialize(obj, forceUseRuntimeType: forceUseRuntimeType);
    }

    public static string ToFormattedJson<T>(this T obj, bool forceUseRuntimeType = false)
    {
        return PlatformJsonSerializer.Serialize(
            obj,
            PlatformJsonSerializer.CurrentOptions.Value.Clone().With(_ => _.WriteIndented = true),
            forceUseRuntimeType);
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

    /// <summary>
    /// Set property of an object event if the property is protected or private
    /// </summary>
    public static TObject SetProperty<TObject, TProp>(this TObject obj, Expression<Func<TObject, TProp>> prop, TProp newValue)
    {
        var propertyInfo = typeof(TObject).GetProperty(prop.GetPropertyName());

        propertyInfo!.SetValue(obj, newValue);

        return obj;
    }

    /// <summary>
    /// Set property of an object event if the property is protected or private
    /// </summary>
    public static TObject SetProperty<TObject, TProp>(this TObject obj, string propName, TProp newValue)
    {
        var propertyInfo = typeof(TObject).GetProperty(propName);

        propertyInfo!.SetValue(obj, newValue);

        return obj;
    }

    public static TProp GetProperty<TObject, TProp>(this TObject obj, string propName)
    {
        var propertyInfo = typeof(TObject).GetProperty(propName);

        return propertyInfo!.GetValue(obj).Cast<TProp>();
    }

    public static object GetProperty<TObject>(this TObject obj, string propName)
    {
        var propertyInfo = typeof(TObject).GetProperty(propName);

        return propertyInfo!.GetValue(obj);
    }

    /// <summary>
    /// Try Set property of an object event if the property is protected or private
    /// </summary>
    public static TObject TrySetProperty<TObject, TProp>(this TObject obj, Expression<Func<TObject, TProp>> prop, TProp newValue)
    {
        var propertyInfo = typeof(TObject).GetProperty(prop.GetPropertyName());
        if (propertyInfo?.GetSetMethod() != null) propertyInfo.SetValue(obj, newValue);

        return obj;
    }

    public static TObject DeepClone<TObject>(this TObject obj)
    {
        return PlatformJsonSerializer.Deserialize<TObject>(PlatformJsonSerializer.Serialize(obj));
    }
}
