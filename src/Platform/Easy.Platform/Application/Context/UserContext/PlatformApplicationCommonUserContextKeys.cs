using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Application.Context.UserContext;

public static class PlatformApplicationCommonUserContextKeys
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

    public static string RequestId(this IDictionary<string, object> context)
    {
        return context.GetValue<string>(RequestIdContextKey);
    }

    public static string UserId(this IDictionary<string, object> context)
    {
        return context.GetValue<string>(UserIdContextKey);
    }

    public static T UserId<T>(this IDictionary<string, object> context)
    {
        return (T)UserId(context, typeof(T));
    }

    public static object UserId(this IDictionary<string, object> context, Type userIdType)
    {
        return context.UserId().ParseToSerializableType(userIdType);
    }

    public static string UserName(this IDictionary<string, object> context)
    {
        return context.GetValue<string>(UserNameContextKey);
    }

    public static string Email(this IDictionary<string, object> context)
    {
        return context.GetValue<string>(EmailContextKey);
    }

    public static List<string> UserRoles(this IDictionary<string, object> context)
    {
        return context.GetValue<List<string>>(UserRolesContextKey);
    }

    public static string UserFullName(this IDictionary<string, object> context)
    {
        return context.GetValue<string>(UserFullNameContextKey) ?? context.UserCalculatedFullName();
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
        return context.GetValue<string>(UserFirstNameContextKey);
    }

    public static string UserMiddleName(this IDictionary<string, object> context)
    {
        return context.GetValue<string>(UserMiddleNameContextKey);
    }

    public static string UserLastName(this IDictionary<string, object> context)
    {
        return context.GetValue<string>(UserLastNameContextKey);
    }

    public static void SetRequestId(this IDictionary<string, object> context, string value)
    {
        context.SetValue(value, RequestIdContextKey);
    }

    public static void SetUserId(this IDictionary<string, object> context, string value)
    {
        context?.SetValue(value, UserIdContextKey);
    }

    public static TContext SetUserRoles<TContext>(this TContext context, List<string> value) where TContext : IDictionary<string, object>
    {
        context.SetValue(value, UserRolesContextKey);

        return context;
    }

    public static TContext SetEmail<TContext>(this TContext context, string value) where TContext : IDictionary<string, object>
    {
        context.SetValue(value, EmailContextKey);

        return context;
    }

    public static void SetUserName(this IDictionary<string, object> context, string value)
    {
        context.SetValue(value, UserNameContextKey);
    }

    public static void SetUserFullName(this IDictionary<string, object> context, string value)
    {
        context.SetValue(value, UserFullNameContextKey);
    }

    public static void SetUserLastName(this IDictionary<string, object> context, string value)
    {
        context.SetValue(value, UserLastNameContextKey);
    }

    public static void SetUserMiddleName(this IDictionary<string, object> context, string value)
    {
        context.SetValue(value, UserMiddleNameContextKey);
    }

    public static void SetUserFirstName(this IDictionary<string, object> context, string value)
    {
        context.SetValue(value, UserFirstNameContextKey);
    }
}
