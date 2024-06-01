using System.Reflection;

namespace Easy.Platform.Common.Extensions;

public static class TypeExtension
{
    /// <summary>
    /// Determines whether a given type is assignable to a specified generic type.
    /// </summary>
    /// <param name="givenType">The type to check.</param>
    /// <param name="genericType">The generic type to which the given type may be assignable.</param>
    /// <returns>True if the given type is assignable to the generic type, otherwise false.</returns>
    public static bool IsAssignableToGenericType(this Type givenType, Type genericType)
    {
        while (true)
        {
            var givenInterfaceTypes = givenType.GetInterfaces();

            foreach (var givenInterfaceType in givenInterfaceTypes)
                if (givenInterfaceType.IsGenericType && givenInterfaceType.GetGenericTypeDefinition() == genericType)
                    return true;

            if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType) return true;

            var baseType = givenType.BaseType;
            if (baseType == null) return false;

            givenType = baseType;
        }
    }

    /// <summary>
    /// Gets the name of the type or, if the type is generic, gets the generic type name.
    /// </summary>
    /// <param name="t">The type to get the name for.</param>
    /// <returns>The name of the type or the generic type name.</returns>
    public static string GetNameOrGenericTypeName(this Type t)
    {
        if (!t.IsGenericType)
            return t.Name;

        return !t.IsGenericType ? t.Name : GetGenericTypeName(t);
    }

    /// <summary>
    /// Gets the full name of the type or, if the type is generic, gets the generic type full name.
    /// </summary>
    /// <param name="t">The type to get the full name for.</param>
    /// <returns>The name of the type or the generic type full name.</returns>
    public static string GetFullNameOrGenericTypeFullName(this Type t)
    {
        if (!t.IsGenericType)
            return t.FullName;

        return !t.IsGenericType ? t.FullName : GetGenericTypeFullName(t);
    }

    /// <summary>
    /// Gets the generic type name of a type.
    /// </summary>
    /// <param name="t">The type to get the generic type name for.</param>
    /// <returns>The generic type name of the type.</returns>
    public static string GetGenericTypeName(this Type t)
    {
        var genericTypeName = t.GetGenericTypeDefinition().Name;

        var genericTypeClassNameOnly = genericTypeName.Substring(0, genericTypeName.IndexOf('`'));

        var genericArgs = t.GetGenericArguments().Select(GetNameOrGenericTypeName).JoinToString(",");

        return $"{genericTypeClassNameOnly}<{genericArgs}>";
    }

    /// <summary>
    /// Gets the generic type full name of a type.
    /// </summary>
    /// <param name="t">The type to get the generic type full name for.</param>
    /// <returns>The generic type full name of the type.</returns>
    public static string GetGenericTypeFullName(this Type t)
    {
        var genericTypeName = t.GetGenericTypeDefinition().Name;

        var genericTypeClassNameOnly = genericTypeName.Substring(0, genericTypeName.IndexOf('`'));

        var genericArgs = t.GetGenericArguments().Select(GetNameOrGenericTypeName).JoinToString(",");

        return $"{t.Namespace}.{genericTypeClassNameOnly}<{genericArgs}>";
    }

    /// <summary>
    /// Gets all public constant values of a specific type from a type.
    /// </summary>
    /// <typeparam name="T">The type of the constant values to get.</typeparam>
    /// <param name="type">The type to get the constant values from.</param>
    /// <returns>A list of all public constant values of the specified type.</returns>
    public static List<T> GetAllPublicConstantValues<T>(this Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(fi => fi.IsLiteral && fi.FieldType == typeof(T))
            .Select(x => (T)x.GetRawConstantValue())
            .ToList();
    }

    /// <summary>
    /// References:
    /// https://stackoverflow.com/questions/3117090/getinterfaces-returns-generic-interface-type-with-fullname-null/3117293
    /// <br />
    /// This function used to fix when a Type is generic, get Interfaces will lead to missing fullName => lead to register into
    /// ServiceCollection for generic type get errors
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Type FixMissingFullNameGenericType(this Type type)
    {
        if (type.FullName != null)
            return type;

        var typeQualifiedName = type.DeclaringType != null
            ? type.DeclaringType.FullName + "+" + type.Name + ", " + type.Assembly.FullName
            : type.Namespace + "." + type.Name + ", " + type.Assembly.FullName;

        return Type.GetType(typeQualifiedName, true);
    }

    /// <summary>
    /// Checks if all generic arguments from a source type are assignable to a target type.
    /// </summary>
    /// <param name="sourceType">The source type.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns>True if all generic arguments from the source type are assignable to the target type, otherwise false.</returns>
    public static bool MatchGenericArguments(this Type sourceType, Type targetType)
    {
        return targetType.IsGenericType &&
               sourceType.GetGenericArguments()
                   .IsAllItemsMatch(
                       targetType.GetGenericArguments(),
                       (sourceItem, targetItem) => sourceItem.IsAssignableTo(targetItem));
    }

    /// <summary>
    /// Finds the first generic type in the inheritance hierarchy of the given type that matches the specified generic type.
    /// </summary>
    /// <param name="givenType">The type to check.</param>
    /// <param name="genericType">The generic type to match.</param>
    /// <returns>The first matched generic type in the inheritance hierarchy of the given type, or null if no match is found.</returns>
    public static Type FindMatchedGenericType(this Type givenType, Type genericType)
    {
        while (true)
        {
            var givenInterfaceTypes = givenType.GetInterfaces();

            foreach (var givenInterfaceType in givenInterfaceTypes)
                if (givenInterfaceType.IsGenericType && givenInterfaceType.GetGenericTypeDefinition() == genericType.GetGenericTypeDefinition())
                    return givenInterfaceType;

            if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType.GetGenericTypeDefinition()) return givenType;

            var baseType = givenType.BaseType;
            if (baseType == null) return null;

            givenType = baseType;
        }
    }

    public static object GetDefaultValue(this Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    /// Determines whether the given type is using a specified type in any of its public constructors.
    /// </summary>
    /// <typeparam name="T">The type to check for in the constructors.</typeparam>
    /// <param name="type">The type to inspect its constructors.</param>
    /// <returns>True if the given type is using the specified type in any of its public constructors, otherwise false.</returns>
    public static bool IsUsingGivenTypeViaConstructor<T>(this Type type)
    {
        return type
            .GetConstructors()
            .Any(p => p.IsPublic && p.GetParameters().Any(p => p.ParameterType.IsAssignableTo(typeof(T))));
    }
}
