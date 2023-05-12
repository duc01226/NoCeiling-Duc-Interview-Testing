using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Application.Context.UserContext;

public interface IPlatformApplicationUserContext
{
    T GetValue<T>(string contextKey = "");

    object GetValue(Type valueType, string contextKey = "");

    void SetValue(object value, string contextKey = "");

    List<string> GetAllKeys();

    Dictionary<string, object> GetAllKeyValues();

    void Clear();
}

public static class PlatformApplicationUserContextExtensions
{
    public static IPlatformApplicationUserContext SetValues(this IPlatformApplicationUserContext context, IEnumerable<KeyValuePair<string, object>> values)
    {
        values.ForEach(p => context.SetValue(p.Value, p.Key));

        return context;
    }
}
