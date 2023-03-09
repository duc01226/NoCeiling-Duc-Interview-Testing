using System.Reflection;

namespace Easy.Platform.Common.Extensions;

public static class TypeExtension
{
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

    public static string GetNameOrGenericTypeName(this Type t)
    {
        if (!t.IsGenericType)
            return t.Name;

        return !t.IsGenericType ? t.Name : GetGenericTypeName(t);
    }

    public static string GetGenericTypeName(this Type t)
    {
        var genericTypeName = t.GetGenericTypeDefinition().Name;

        var genericTypeClassNameOnly = genericTypeName.Substring(0, genericTypeName.IndexOf('`'));

        var genericArgs = t.GetGenericArguments().Select(GetNameOrGenericTypeName).JoinToString(",");

        return genericTypeClassNameOnly + "<" + genericArgs + ">";
    }

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
    /// Check if all GenericArguments from sourceType must be assignable to targetType
    /// </summary>
    public static bool MatchGenericArguments(this Type sourceType, Type targetType)
    {
        return targetType.IsGenericType &&
               sourceType.GetGenericArguments()
                   .ItemsMatch(
                       targetType.GetGenericArguments(),
                       (sourceItem, targetItem) => sourceItem.IsAssignableTo(targetItem));
    }

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
        if (type.IsValueType) return Activator.CreateInstance(type);
        return null;
    }
}
