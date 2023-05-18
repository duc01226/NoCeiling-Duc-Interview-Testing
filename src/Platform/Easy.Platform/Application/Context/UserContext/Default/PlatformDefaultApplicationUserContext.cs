using System.Collections.Concurrent;
using System.ComponentModel;

namespace Easy.Platform.Application.Context.UserContext.Default;

public class PlatformDefaultApplicationUserContext : IPlatformApplicationUserContext
{
    private readonly ConcurrentDictionary<string, object> userContextData = new();

    public T GetValue<T>(string contextKey = "")
    {
        return (T)userContextData.GetValueOrDefault(contextKey);
    }

    public object GetValue(Type valueType, string contextKey = "")
    {
        return TypeDescriptor.GetConverter(valueType).ConvertFrom(userContextData.GetValueOrDefault(contextKey));
    }

    public void SetValue(object value, string contextKey = "")
    {
        userContextData[contextKey] = value;
    }

    public List<string> GetAllKeys()
    {
        return userContextData.Keys.ToList();
    }

    public Dictionary<string, object> GetAllKeyValues()
    {
        return new Dictionary<string, object>(userContextData.Select(p => new KeyValuePair<string, object>(p.Key, p.Value)));
    }

    public void Clear()
    {
        userContextData.Clear();
    }
}
