using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;

namespace Easy.Platform.Common.Extensions;

public static class ObjectGeneralExtension
{
    /// <summary>
    /// Checks if the values of two objects are different.
    /// </summary>
    /// <typeparam name="T1">The type of the first object.</typeparam>
    /// <typeparam name="T2">The type of the second object.</typeparam>
    /// <param name="obj1">The first object.</param>
    /// <param name="obj2">The second object.</param>
    /// <returns>True if the values are different, false otherwise.</returns>
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

    /// <summary>
    /// Checks if the values of two objects are equal.
    /// </summary>
    /// <typeparam name="T1">The type of the first object.</typeparam>
    /// <typeparam name="T2">The type of the second object.</typeparam>
    /// <param name="obj1">The first object.</param>
    /// <param name="obj2">The second object.</param>
    /// <returns>True if the values are equal, false otherwise.</returns>
    public static bool IsValuesEqual<T1, T2>(this T1 obj1, T2 obj2)
    {
        return !IsValuesDifferent(obj1, obj2);
    }

    /// <summary>
    /// Casts the given object to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the object to.</typeparam>
    /// <param name="obj">The object to cast.</param>
    /// <returns>The object cast to the specified type, or null if the cast is not possible.</returns>
    public static T As<T>(this object obj) where T : class
    {
        return obj as T;
    }

    /// <summary>
    /// Casts the given object to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the object to.</typeparam>
    /// <param name="obj">The object to be cast.</param>
    /// <returns>The casted object of type T.</returns>
    /// <exception cref="InvalidCastException">Thrown when the object cannot be cast to the specified type.</exception>
    public static T Cast<T>(this object obj)
    {
        return (T)obj;
    }

    /// <summary>
    /// Tries to cast the given object to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the object to.</typeparam>
    /// <param name="obj">The object to be cast.</param>
    /// <param name="castResult">The result of the cast operation. This will be default(T) if the cast is not successful.</param>
    /// <returns>True if the cast was successful, false otherwise.</returns>
    public static bool TryCast<T>(this object obj, out T castResult)
    {
        try
        {
            castResult = (T)obj;

            return true;
        }
        catch (Exception)
        {
            castResult = default;
            return false;
        }
    }

    public static T[] BoxedInArray<T>(this T obj)
    {
        return
        [
            obj
        ];
    }

    public static List<T> BoxedInList<T>(this T obj)
    {
        return Util.ListBuilder.New(obj);
    }

    public static string ToJson<T>(this T obj, bool forceUseRuntimeType = false)
    {
        return PlatformJsonSerializer.Serialize(obj, forceUseRuntimeType: forceUseRuntimeType);
    }

    public static T JsonDeserialize<T>(this string jsonStr)
    {
        return PlatformJsonSerializer.Deserialize<T>(jsonStr);
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

    /// <summary>
    /// Retrieves the value of a specified property from the given object.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TProp">The type of the property value.</typeparam>
    /// <param name="obj">The object from which to retrieve the property value.</param>
    /// <param name="propName">The name of the property.</param>
    /// <returns>The value of the specified property cast to the type TProp.</returns>
    /// <exception cref="TargetException">Thrown when the object does not match the target type, or when the property is an instance property but obj is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not exist in the source object.</exception>
    /// <exception cref="InvalidCastException">Thrown when the property value cannot be cast to the specified type.</exception>
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

    public static bool Is<TObject>(this TObject obj, Expression<Func<TObject, bool>> expr)
    {
        return expr.Compile().Invoke(obj);
    }

    public static bool Is<TObject>(this TObject obj, Func<TObject, bool> func)
    {
        return func(obj);
    }
}
