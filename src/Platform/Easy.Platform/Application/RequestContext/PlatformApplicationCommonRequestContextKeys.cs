using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Application.RequestContext;

public static class PlatformApplicationCommonRequestContextKeys
{
    public const string RequestIdContextKey = "RequestId";
    public const string UserIdContextKey = "UserId";
    public const string UserNameContextKey = "UserName";
    public const string EmailContextKey = "Email";
    public const string UserRolesContextKey = "UserRoles";
    public const string UserFullNameContextKey = "UserFullName";
    public const string UserFirstNameContextKey = "UserFirstName";
    public const string UserMiddleNameContextKey = "UserMiddleName";
    public const string UserLastNameContextKey = "UserLastName";
    public const string IsSeedingTestingDataKey = "IsSeedingTestingData";

    public static string RequestId(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<string>(RequestIdContextKey);
    }

    public static string UserId(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<string>(UserIdContextKey);
    }

    public static bool IsSeedingTestingData(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<bool?>(IsSeedingTestingDataKey) == true;
    }

    public static T UserId<T>(this IDictionary<string, object> context)
    {
        return (T)context.UserId(typeof(T));
    }

    public static object UserId(this IDictionary<string, object> context, Type userIdType)
    {
        return context.UserId().ParseToSerializableType(userIdType);
    }

    public static string UserName(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<string>(UserNameContextKey);
    }

    public static string Email(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<string>(EmailContextKey);
    }

    public static List<string> UserRoles(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<List<string>>(UserRolesContextKey) ?? [];
    }

    public static string UserFullName(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<string>(UserFullNameContextKey) ?? context.UserCalculatedFullName();
    }

    public static string UserCalculatedFullName(this IDictionary<string, object> context)
    {
        var userFirstNamePart = ((context.UserFirstName() ?? string.Empty) + " ").Trim();
        var userMiddleNamePart = ((context.UserMiddleName() ?? string.Empty) + " ").Trim();
        var userLastNamePart = context.UserLastName() ?? string.Empty;

        return $"{userFirstNamePart} {userMiddleNamePart} {userLastNamePart}";
    }

    public static string UserFirstName(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<string>(UserFirstNameContextKey);
    }

    public static string UserMiddleName(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<string>(UserMiddleNameContextKey);
    }

    public static string UserLastName(this IDictionary<string, object> context)
    {
        return context.GetRequestContextValue<string>(UserLastNameContextKey);
    }

    public static TContext SetRequestId<TContext>(this TContext context, string value) where TContext : IDictionary<string, object>
    {
        context?.SetRequestContextValue(value, RequestIdContextKey);

        return context;
    }

    public static TContext SetUserId<TContext>(this TContext context, string value) where TContext : IDictionary<string, object>
    {
        context?.SetRequestContextValue(value, UserIdContextKey);

        return context;
    }

    public static TContext SetUserRoles<TContext>(this TContext context, List<string> value) where TContext : IDictionary<string, object>
    {
        context.SetRequestContextValue(value, UserRolesContextKey);

        return context;
    }

    public static TContext SetEmail<TContext>(this TContext context, string value) where TContext : IDictionary<string, object>
    {
        context.SetRequestContextValue(value, EmailContextKey);

        return context;
    }

    public static void SetUserName(this IDictionary<string, object> context, string value)
    {
        context.SetRequestContextValue(value, UserNameContextKey);
    }

    public static void SetUserFullName(this IDictionary<string, object> context, string value)
    {
        context.SetRequestContextValue(value, UserFullNameContextKey);
    }

    public static void SetUserLastName(this IDictionary<string, object> context, string value)
    {
        context.SetRequestContextValue(value, UserLastNameContextKey);
    }

    public static void SetUserMiddleName(this IDictionary<string, object> context, string value)
    {
        context.SetRequestContextValue(value, UserMiddleNameContextKey);
    }

    public static void SetUserFirstName(this IDictionary<string, object> context, string value)
    {
        context.SetRequestContextValue(value, UserFirstNameContextKey);
    }

    public static void SetIsSeedingTestingData(this IDictionary<string, object> context, bool value)
    {
        context.SetRequestContextValue(value, IsSeedingTestingDataKey);
    }
}
