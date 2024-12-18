using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Easy.Platform.Common.JsonSerialization;

namespace Easy.Platform.Common.Extensions;

public static class ObjectGeneralExtension
{
    private static readonly ConcurrentDictionary<Type, List<PropertyInfo>> CachedIsValuesDifferentTypeToPropsDict = new();
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<Type, bool>> CachedCheckTypeIsAssignableFromDict = new();
    private static readonly ConcurrentDictionary<string, List<PropertyInfo>> CachedGetChangedFieldsTypeToPropsDict = new();

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
        return InternalIsValuesDifferent(obj1, obj2, typeof(T1), typeof(T2));

        static bool InternalIsValuesDifferent(object obj1, object obj2, Type obj1CheckType, Type obj2CheckType)
        {
            if ((obj1 == null && obj2 != null) || (obj2 == null && obj1 != null))
                return true;

            // Case both obj is null
            if (obj1 == null)
                return false;

            var obj1Type = obj1.GetType()
                .Pipe(obj1RuntimeType => obj1RuntimeType != obj1CheckType && obj1CheckType == typeof(object) ? obj1RuntimeType : obj1CheckType);
            var obj2Type = obj2.GetType()
                .Pipe(obj2RuntimeType => obj2RuntimeType != obj2CheckType && obj2CheckType == typeof(object) ? obj2RuntimeType : obj2CheckType);

            if (obj1 is DateTime obj1DateTime && obj2 is DateTime obj2DateTime)
                return obj1DateTime.IsDifferentIgnoringNanoseconds(obj2DateTime);

            if (obj1Type == obj2Type &&
                (obj1Type.IsPrimitive || obj1Type.IsValueType || obj1Type == typeof(string)))
                return !obj1.Equals(obj2);

            if (obj1Type != obj2Type || obj1Type == typeof(object))
                return PlatformJsonSerializer.Serialize(obj1) != PlatformJsonSerializer.Serialize(obj2);

            // Handle collections (Collection<T>, List<T>, Dictionary<TKey, TValue>, etc.)
            if (IsAssignableFromIEnumerable(obj1Type))
            {
                // Handle IList
                if (IsAssignableFromIList(obj1Type))
                {
                    var obj1List = obj1 as IList;
                    var obj2List = obj2 as IList;

                    if (obj1List!.Count != obj2List!.Count)
                        return true;

                    for (var i = 0; i < obj1List.Count; i++)
                    {
                        if (IsValuesDifferent(obj1List[i], obj2List[i]))
                            return true;
                    }

                    return false;
                }

                // Handle ICollection
                if (IsAssignableFromICollection(obj1Type))
                {
                    var obj1Collection = obj1 as ICollection;
                    var obj2Collection = obj2 as ICollection;

                    if (obj1Collection!.Count != obj2Collection!.Count)
                        return true;

                    var obj1List = obj1Collection.ToObjectList();
                    var obj2List = obj2Collection.ToObjectList();

                    for (var i = 0; i < obj1List.Count; i++)
                    {
                        if (IsValuesDifferent(obj1List[i], obj2List[i]))
                            return true;
                    }

                    return false;
                }

                // Handle Dictionary < TKey, TValue >
                if (IsAssignableFromIDictionary(obj1Type))
                {
                    var obj1Dict = obj1 as IDictionary;
                    var obj2Dict = obj2 as IDictionary;

                    if (obj1Dict!.Count != obj2Dict!.Count)
                        return true;

                    var obj1List = obj1Dict.ToEntryItemList();
                    var obj2List = obj2Dict.ToEntryItemList();

                    for (var i = 0; i < obj1List.Count; i++)
                    {
                        if (IsValuesDifferent(obj1List[i].Value, obj2List[i].Value) ||
                            IsValuesDifferent(obj1List[i].Key, obj2List[i].Key))
                            return true;
                    }

                    return false;
                }

                return PlatformJsonSerializer.Serialize(obj1) != PlatformJsonSerializer.Serialize(obj2);
            }

            // Handle classes (other reference types)
            if (obj1Type.IsClass)
            {
                var propInfos = CachedIsValuesDifferentTypeToPropsDict.GetOrAdd(
                    obj1Type,
                    type => CachedIsValuesDifferentTypeToPropsDictFactory(type));

                // Deep clone each field
                foreach (var propInfo in propInfos)
                {
                    var value1 = propInfo.GetValue(obj1);
                    var value2 = propInfo.GetValue(obj2);

                    // Ensure the values are compared using the appropriate method
                    if (value1 != null && value2 != null)
                    {
                        if (value1.GetType() != value2.GetType())
                        {
                            if (PlatformJsonSerializer.Serialize(value1) != PlatformJsonSerializer.Serialize(value2))
                                return true;
                        }
                        else if (InternalIsValuesDifferent(
                            value1,
                            value2,
                            propInfo.PropertyType,
                            propInfo.PropertyType)) // Call with the correct types
                            return true;
                    }
                    else if (value1 != value2) // Handle null comparison
                        return true;
                }

                return false;
            }

            return PlatformJsonSerializer.Serialize(obj1) != PlatformJsonSerializer.Serialize(obj2);
        }
    }

    private static List<PropertyInfo> CachedIsValuesDifferentTypeToPropsDictFactory(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(
                propInfo => propInfo.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                            propInfo.GetCustomAttribute<PlatformIgnoreCheckValueDiffAttribute>() == null)
            .ToList();
    }

    private static bool IsAssignableFromIDictionary(Type objType)
    {
        return CachedCheckTypeIsAssignableFromDict
            .GetOrAdd(typeof(IDictionary), p => new ConcurrentDictionary<Type, bool>())
            .GetOrAdd(objType, objType => typeof(IDictionary).IsAssignableFrom(objType));
    }

    private static bool IsAssignableFromICollection(Type objType)
    {
        return CachedCheckTypeIsAssignableFromDict
            .GetOrAdd(typeof(ICollection), p => new ConcurrentDictionary<Type, bool>())
            .GetOrAdd(objType, objType => typeof(ICollection).IsAssignableFrom(objType));
    }

    private static bool IsAssignableFromIList(Type objType)
    {
        return CachedCheckTypeIsAssignableFromDict
            .GetOrAdd(typeof(IList), p => new ConcurrentDictionary<Type, bool>())
            .GetOrAdd(objType, objType => typeof(IList).IsAssignableFrom(objType));
    }

    private static bool IsAssignableFromIEnumerable(Type objType)
    {
        return CachedCheckTypeIsAssignableFromDict
            .GetOrAdd(typeof(IEnumerable), p => new ConcurrentDictionary<Type, bool>())
            .GetOrAdd(objType, objType => typeof(IEnumerable).IsAssignableFrom(objType));
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
    /// Gets a dictionary of changed fields between two objects of the same type.
    /// </summary>
    /// <typeparam name="T">The type of the objects to compare.</typeparam>
    /// <param name="updatedObject">The updated object.</param>
    /// <param name="originalObject">The originalObject to compare against.</param>
    /// <param name="propFilterPredicate">propFilterPredicate</param>
    /// <returns>A dictionary with property names as keys and updated values as values.</returns>
    public static Dictionary<string, object> GetChangedFields<T>(this T updatedObject, T originalObject, Expression<Func<PropertyInfo, bool>> propFilterPredicate = null)
    {
        if (originalObject is null || updatedObject is null || ReferenceEquals(updatedObject, originalObject))
            return null;

        var updatedObjectType = updatedObject.GetType();

        var useType = updatedObjectType != typeof(T) &&
                      updatedObjectType == originalObject.GetType() &&
                      updatedObjectType.IsAssignableTo(typeof(T))
            ? updatedObjectType
            : typeof(T);

        var properties = CachedGetChangedFieldsTypeToPropsDict.GetOrAdd(
            $"{useType.FullName ?? useType.Name}{propFilterPredicate}",
            _ => CachedIsValuesDifferentTypeToPropsDictFactory(useType)
                .Where(prop => prop.GetSetMethod(true) != null)
                .WhereIf(propFilterPredicate != null, propFilterPredicate!)
                .ToList());
        if (properties.Count == 0) return null;

        var changedFields = properties
            .Select(
                prop =>
                {
                    var updatedObjectValue = prop.GetValue(updatedObject);
                    var originalObjectValue = prop.GetValue(originalObject);

                    return (propName: prop.Name, updatedObjectValue, isValuesDifferent: IsValuesDifferent(updatedObjectValue, originalObjectValue));
                })
            .Where(p => p.isValuesDifferent)
            .Select(p => new KeyValuePair<string, object>(p.propName, p.updatedObjectValue));

        return new Dictionary<string, object>(changedFields);
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
        return [obj];
    }

    public static string ToJson<T>(this T obj, bool forceUseRuntimeType = false)
    {
        return PlatformJsonSerializer.Serialize(obj, forceUseRuntimeType);
    }

    public static T JsonDeserialize<T>(this string jsonStr)
    {
        return PlatformJsonSerializer.Deserialize<T>(jsonStr);
    }

    public static string ToFormattedJson<T>(this T obj, bool forceUseRuntimeType = false)
    {
        return PlatformJsonSerializer.Serialize(
            obj,
            PlatformJsonSerializer.CurrentOptions.Value.Clone().With(options => options.WriteIndented = true),
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
        // ReSharper disable once ExpressionIsAlwaysNull
        return obj is null
            ? obj
            : PlatformJsonSerializer.Deserialize<TObject>(PlatformJsonSerializer.Serialize(obj));
    }

    public static object DeepClone(this object obj, Type objType)
    {
        // ReSharper disable once ExpressionIsAlwaysNull
        return obj is null
            ? obj
            : PlatformJsonSerializer.Deserialize(PlatformJsonSerializer.Serialize(obj), objType);
    }

    public static bool Is<TObject>(this TObject obj, Expression<Func<TObject, bool>> expr)
    {
        return expr.Compile().Invoke(obj);
    }

    public static TReturn Get<TObject, TReturn>(this TObject obj, Expression<Func<TObject, TReturn>> expr)
    {
        return expr.Compile().Invoke(obj);
    }

    public static bool Is<TObject>(this TObject obj, Func<TObject, bool> func)
    {
        return func(obj);
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class PlatformIgnoreCheckValueDiffAttribute : Attribute
{
}
