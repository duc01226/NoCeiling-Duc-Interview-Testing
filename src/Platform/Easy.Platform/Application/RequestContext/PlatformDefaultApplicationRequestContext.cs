using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.RequestContext;

namespace Easy.Platform.Application.RequestContext;

public class PlatformDefaultApplicationRequestContext : IPlatformApplicationRequestContext
{
    private static readonly MethodInfo GetValueByGenericTypeMethodInfo =
        typeof(PlatformDefaultApplicationRequestContext).GetMethods()
            .First(p => p.IsGenericMethod && p.Name == nameof(GetValue) && p.GetGenericArguments().Length == 1 && p.IsPublic);

    protected readonly ConcurrentDictionary<string, object> RequestContextData = new();

    public T GetValue<T>(string contextKey)
    {
        ArgumentNullException.ThrowIfNull(contextKey);

        if (PlatformRequestContextHelper.TryGetValue(RequestContextData, contextKey, out T item)) return item;

        return default;
    }

    public object GetValue(Type valueType, string contextKey)
    {
        return GetValueByGenericTypeMethodInfo
            .MakeGenericMethod(valueType)
            .Invoke(this, parameters: [contextKey]);
    }

    public void SetValue(object value, string contextKey)
    {
        ArgumentNullException.ThrowIfNull(contextKey);

        RequestContextData.Upsert(contextKey, value);
    }

    public List<string> GetAllKeys()
    {
        return [.. RequestContextData.Keys];
    }

    public Dictionary<string, object> GetAllKeyValues(HashSet<string>? ignoreKeys = null)
    {
        return GetAllKeys()
            .WhereIf(ignoreKeys?.Any() == true, key => !ignoreKeys.Contains(key))
            .Select(key => new KeyValuePair<string, object>(key, GetValue<object>(key)))
            .ToDictionary(p => p.Key, p => p.Value);
    }

    public void Add(KeyValuePair<string, object> item)
    {
        RequestContextData.Upsert(item.Key, item.Value);
    }

    public void Clear()
    {
        RequestContextData.Clear();
    }

    public bool Contains(KeyValuePair<string, object> item)
    {
        return RequestContextData.Contains(item);
    }

    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
    {
        RequestContextData.ToList().CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, object> item)
    {
        return RequestContextData.Remove(item.Key, out _);
    }

    public int Count => RequestContextData.Count;
    public bool IsReadOnly => false;

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        return RequestContextData.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(string key, object value)
    {
        RequestContextData.Upsert(key, value);
    }

    public bool ContainsKey(string key)
    {
        return RequestContextData.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        return RequestContextData.Remove(key, out _);
    }

    public bool TryGetValue(string key, out object value)
    {
        return RequestContextData.TryGetValue(key, out value);
    }

    public object this[string key]
    {
        get => RequestContextData[key];
        set => RequestContextData[key] = value;
    }

    public ICollection<string> Keys => RequestContextData.Keys;
    public ICollection<object> Values => RequestContextData.Values;
}
