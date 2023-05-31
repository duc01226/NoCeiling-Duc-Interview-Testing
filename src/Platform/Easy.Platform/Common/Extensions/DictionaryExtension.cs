using System.Collections;
using System.Collections.Concurrent;

namespace Easy.Platform.Common.Extensions;

public static class DictionaryExtension
{
    /// <summary>
    /// Insert if item is not existed. Update if item is existed
    /// </summary>
    public static TDic Upsert<TDic, TKey, TValue>(this TDic dictionary, TKey key, TValue value) where TDic : IDictionary<TKey, TValue>
    {
        if (dictionary.ContainsKey(key))
            dictionary[key] = value;
        else
            dictionary.Add(key, value);

        return dictionary;
    }

    /// <inheritdoc cref="Upsert{TDic,TKey,TValue}" />
    public static IDictionary<TKey, TValue> Upsert<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.ContainsKey(key))
            dictionary[key] = value;
        else
            dictionary.Add(key, value);

        return dictionary;
    }

    public static ConcurrentDictionary<TKey, TValue> Upsert<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        dictionary.AddOrUpdate(key, key => value, (key, currentValue) => value);

        return dictionary;
    }

    /// <summary>
    /// Try get value from key. Return default value if key is not existing
    /// </summary>
    public static TValue TryGetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        if (dictionary.TryGetValue(key, out var value)) return value;

        return default;
    }

    /// <summary>
    /// Converts a dictionary to another one with string-ified keys.
    /// </summary>
    /// <param name="dictionary">The input dictionary.</param>
    /// <returns>A dictionary with string-ified keys.</returns>
    public static Dictionary<string, object?> ToStringObjectDictionary(this IDictionary dictionary)
    {
        var result = new Dictionary<string, object?>(dictionary.Count);

        foreach (var key in dictionary.Keys)
            if (key is not null)
            {
                var keyString = key.ToString();
                var value = dictionary[key];

                if (keyString is not null) result.Add(keyString, value);
            }

        return result;
    }
}
