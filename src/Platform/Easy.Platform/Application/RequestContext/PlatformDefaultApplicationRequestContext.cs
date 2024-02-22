using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.RequestContext;

namespace Easy.Platform.Application.RequestContext;

public class PlatformDefaultApplicationRequestContext : IPlatformApplicationRequestContext
{
    protected readonly ConcurrentDictionary<string, object> UserContextData = new();
    private readonly MethodInfo getValueByGenericTypeMethodInfo;

    public PlatformDefaultApplicationRequestContext()
    {
        getValueByGenericTypeMethodInfo =
            GetType().GetMethods().First(p => p.IsGenericMethod && p.Name == nameof(GetValue) && p.GetGenericArguments().Length == 1 && p.IsPublic);
    }

    public T GetValue<T>(string contextKey)
    {
        ArgumentNullException.ThrowIfNull(contextKey);

        if (PlatformRequestContextHelper.TryGetValue(UserContextData, contextKey, out T item)) return item;

        return default;
    }

    public object GetValue(Type valueType, string contextKey)
    {
        return getValueByGenericTypeMethodInfo
            .MakeGenericMethod(valueType)
            .Invoke(this, parameters: [contextKey]);
    }

    public void SetValue(object value, string contextKey)
    {
        ArgumentNullException.ThrowIfNull(contextKey);

        UserContextData.Upsert(contextKey, value);
    }

    public List<string> GetAllKeys()
    {
        return [.. UserContextData.Keys];
    }

    public Dictionary<string, object> GetAllKeyValues()
    {
        return GetAllKeys().Select(key => new KeyValuePair<string, object>(key, GetValue<object>(key))).ToDictionary(p => p.Key, p => p.Value);
    }

    public void Add(KeyValuePair<string, object> item)
    {
        UserContextData.Upsert(item.Key, item.Value);
    }

    public void Clear()
    {
        UserContextData.Clear();
    }

    public bool Contains(KeyValuePair<string, object> item)
    {
        return UserContextData.Contains(item);
    }

    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
    {
        UserContextData.ToList().CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, object> item)
    {
        return UserContextData.Remove(item.Key, out _);
    }

    public int Count => UserContextData.Count;
    public bool IsReadOnly => false;

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        return UserContextData.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(string key, object value)
    {
        UserContextData.Upsert(key, value);
    }

    public bool ContainsKey(string key)
    {
        return UserContextData.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        return UserContextData.Remove(key, out _);
    }

    public bool TryGetValue(string key, out object value)
    {
        return UserContextData.TryGetValue(key, out value);
    }

    public object this[string key]
    {
        get => UserContextData[key];
        set => UserContextData[key] = value;
    }

    public ICollection<string> Keys => UserContextData.Keys;
    public ICollection<object> Values => UserContextData.Values;
}
