using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.RequestContext;

namespace Easy.Platform.Application.Context.UserContext;

/// <summary>
/// This is the current context data in the current local thread from IPlatformApplicationUserContextAccessor. Please never save this context as a property in any Service/Command/Handler.
/// Should use the IPlatformApplicationUserContextAccessor.Current to get the data.
/// </summary>
public interface IPlatformApplicationUserContext : IDictionary<string, object>
{
    T GetValue<T>(string contextKey);

    object GetValue(Type valueType, string contextKey);

    void SetValue(object value, string contextKey);

    List<string> GetAllKeys();

    Dictionary<string, object> GetAllKeyValues();

    new void Clear();
}

public static class PlatformApplicationUserContextExtensions
{
    public static IPlatformApplicationUserContext SetValues(this IPlatformApplicationUserContext context, IDictionary<string, object> values)
    {
        values.ForEach(p => context.SetValue(p.Value, p.Key));

        return context;
    }

    public static T GetValue<T>(this IDictionary<string, object> context, string contextKey)
    {
        if (contextKey == null)
            throw new ArgumentNullException(nameof(contextKey));

        if (context is IPlatformApplicationUserContext userContext)
            return userContext.GetValue<T>(contextKey);
        if (PlatformRequestContextHelper.TryGetValue(context, contextKey, out T item))
            return item;

        return default;
    }

    public static void SetValue(this IDictionary<string, object> context, object value, string contextKey)
    {
        if (contextKey == null)
            throw new ArgumentNullException(nameof(contextKey));

        if (context is IPlatformApplicationUserContext userContext)
            userContext.SetValue(value, contextKey);
        else
            context.Upsert(contextKey, value);
    }
}
