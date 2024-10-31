using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.RequestContext;
using Type = System.Type;

namespace Easy.Platform.Application.RequestContext;

public class PlatformDefaultApplicationRequestContext : IPlatformApplicationRequestContext
{
    private static readonly MethodInfo GetValueByGenericTypeMethodInfo =
        typeof(PlatformDefaultApplicationRequestContext).GetMethods()
            .First(p => p.IsGenericMethod && p.Name == nameof(GetValue) && p.GetGenericArguments().Length == 1 && p.IsPublic);

    protected readonly IPlatformApplicationSettingContext ApplicationSettingContext;
    protected readonly ConcurrentDictionary<string, object?> FullRequestContextData = new();
    protected readonly ConcurrentDictionary<string, object?> IgnoreRequestContextKeysRequestContextData = new();

    public PlatformDefaultApplicationRequestContext(IPlatformApplicationSettingContext applicationSettingContext)
    {
        ApplicationSettingContext = applicationSettingContext;
    }

    public T GetValue<T>(string contextKey)
    {
        ArgumentNullException.ThrowIfNull(contextKey);

        if (PlatformRequestContextHelper.TryGetValue(FullRequestContextData, contextKey, out T item)) return item;

        return default;
    }

    public object GetValue(Type valueType, string contextKey)
    {
        return GetValueByGenericTypeMethodInfo
            .MakeGenericMethod(valueType)
            .Invoke(this, parameters: [contextKey]);
    }

    public void SetValue(object? value, string contextKey)
    {
        ArgumentNullException.ThrowIfNull(contextKey);

        FullRequestContextData.Upsert(contextKey, value);
        if (!ApplicationSettingContext.GetIgnoreRequestContextKeys().Contains(contextKey))
            IgnoreRequestContextKeysRequestContextData.Upsert(contextKey, value);
    }

    public List<string> GetAllIncludeIgnoredKeys()
    {
        return FullRequestContextData.Keys.ToList();
    }

    public IDictionary<string, object?> GetAllIncludeIgnoredKeyValues()
    {
        return FullRequestContextData;
    }

    public List<string> GetAllKeys()
    {
        return IgnoreRequestContextKeysRequestContextData.Keys.ToList();
    }

    public IDictionary<string, object?> GetAllKeyValues()
    {
        return IgnoreRequestContextKeysRequestContextData;
    }

    public void Add(KeyValuePair<string, object> item)
    {
        FullRequestContextData.Upsert(item.Key, item.Value);
        if (!ApplicationSettingContext.GetIgnoreRequestContextKeys().Contains(item.Key))
            IgnoreRequestContextKeysRequestContextData.Upsert(item.Key, item.Value);
    }

    public void Clear()
    {
        FullRequestContextData.Clear();
        IgnoreRequestContextKeysRequestContextData.Clear();
    }

    public bool Contains(KeyValuePair<string, object> item)
    {
        // ReSharper disable once UsageOfDefaultStructEquality
        return FullRequestContextData.Contains(item);
    }

    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
    {
        FullRequestContextData.ToList().CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, object> item)
    {
        IgnoreRequestContextKeysRequestContextData.Remove(item.Key, out _);
        return FullRequestContextData.Remove(item.Key, out _);
    }

    public int Count => FullRequestContextData.Count;
    public bool IsReadOnly => false;

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        return FullRequestContextData.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(string key, object value)
    {
        FullRequestContextData.Upsert(key, value);
        if (!ApplicationSettingContext.GetIgnoreRequestContextKeys().Contains(key))
            IgnoreRequestContextKeysRequestContextData.Upsert(key, value);
    }

    public bool ContainsKey(string key)
    {
        return FullRequestContextData.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        IgnoreRequestContextKeysRequestContextData.Remove(key, out _);
        return FullRequestContextData.Remove(key, out _);
    }

    public bool TryGetValue(string key, out object value)
    {
        return FullRequestContextData.TryGetValue(key, out value);
    }

    public object this[string key]
    {
        get => FullRequestContextData[key];
        set
        {
            FullRequestContextData[key] = value;
            if (!ApplicationSettingContext.GetIgnoreRequestContextKeys().Contains(key))
                IgnoreRequestContextKeysRequestContextData.Upsert(key, value);
        }
    }

    public ICollection<string> Keys => FullRequestContextData.Keys;
    public ICollection<object> Values => FullRequestContextData.Values;
}
