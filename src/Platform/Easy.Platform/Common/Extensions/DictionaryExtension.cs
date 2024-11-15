using System.Collections;
using System.Collections.Concurrent;

namespace Easy.Platform.Common.Extensions;

public static class DictionaryExtension
{
    /// <summary>
    /// Inserts or updates the specified key-value pair in the provided dictionary.
    /// If the key already exists, the associated value is updated; otherwise, a new key-value pair is added.
    /// </summary>
    /// <typeparam name="TDic">The type of dictionary that implements <see cref="IDictionary{TKey, TValue}" />.</typeparam>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to insert or update the key-value pair in.</param>
    /// <param name="key">The key to insert or update.</param>
    /// <param name="value">The value associated with the key.</param>
    /// <returns>
    /// The dictionary after the insertion or update operation.
    /// </returns>
    /// <remarks>
    /// If the dictionary already contains the specified key, the associated value is updated.
    /// If the key is not present, a new key-value pair is added to the dictionary.
    /// </remarks>
    public static TDic Upsert<TDic, TKey, TValue>(this TDic dictionary, TKey key, TValue value) where TDic : IDictionary<TKey, TValue>
    {
        if (dictionary.ContainsKey(key))
            dictionary[key] = value;
        else
            dictionary.Add(key, value);

        return dictionary;
    }

    public static TDic UpsertMany<TDic, TKey, TValue>(this TDic dictionary, IDictionary<TKey, TValue> values) where TDic : IDictionary<TKey, TValue>
    {
        values.ForEach(item => dictionary.Upsert(item.Key, item.Value));

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

    /// <summary>
    /// Inserts or updates the specified key-value pair in the provided concurrent dictionary.
    /// If the key already exists, the associated value is updated; otherwise, a new key-value pair is added.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the concurrent dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the concurrent dictionary.</typeparam>
    /// <param name="dictionary">The concurrent dictionary to insert or update the key-value pair in.</param>
    /// <param name="key">The key to insert or update.</param>
    /// <param name="value">The value associated with the key.</param>
    /// <returns>
    /// The concurrent dictionary after the insertion or update operation.
    /// </returns>
    /// <remarks>
    /// If the concurrent dictionary already contains the specified key, the associated value is updated.
    /// If the key is not present, a new key-value pair is added to the concurrent dictionary.
    /// The insertion or update operation is thread-safe.
    /// </remarks>
    public static ConcurrentDictionary<TKey, TValue> Upsert<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        dictionary.AddOrUpdate(key, key => value, (key, currentValue) => value);

        return dictionary;
    }

    /// <summary>
    /// Try get value from key. Return default value if key is not existing
    /// </summary>
    public static TValue TryGetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default)
    {
        if (dictionary.TryGetValue(key, out var value)) return value;

        return defaultValue;
    }

    public static TValue GetValueOrDefaultIgnoreCase<TValue>(this IDictionary<string, TValue> dictionary, string key, TValue defaultValue = default)
    {
        if (dictionary.TryGetValue(key, out var value)) return value;
        if (dictionary.TryGetValue(key.ToLower(), out var fromToLowerKeyValue)) return fromToLowerKeyValue;
        if (dictionary.TryGetValue(key.ToUpper(), out var fromToUpperKeyValue)) return fromToUpperKeyValue;

        return defaultValue;
    }

    /// <summary>
    /// Converts a dictionary to another one with string-ified keys.
    /// </summary>
    /// <param name="dictionary">The input dictionary.</param>
    /// <returns>A dictionary with string-ified keys.</returns>
    public static Dictionary<string, object> ToStringObjectDictionary(this IDictionary dictionary)
    {
        var result = new Dictionary<string, object>(dictionary.Count);

        foreach (var key in dictionary.Keys)
            if (key is not null)
            {
                var keyString = key.ToString();
                var value = dictionary[key];

                if (keyString is not null) result.Add(keyString, value);
            }

        return result;
    }

    /// <summary>
    /// Merges two dictionaries, creating a new dictionary with the combined key-value pairs.
    /// If a key exists in both dictionaries, the value from the second dictionary is used.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionaries.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionaries.</typeparam>
    /// <param name="firstDictionary">The first dictionary to be merged.</param>
    /// <param name="secondDictionary">The second dictionary to be merged.</param>
    /// <returns>
    /// A new dictionary containing the combined key-value pairs from both input dictionaries.
    /// </returns>
    /// <remarks>
    /// The method creates a shallow copy of the first dictionary to preserve its original contents.
    /// Key-value pairs from the second dictionary are then added to the copy,
    /// and existing keys are overwritten with values from the second dictionary.
    /// </remarks>
    public static Dictionary<TKey, TValue> Merge<TKey, TValue>(this IDictionary<TKey, TValue> firstDictionary, IDictionary<TKey, TValue> secondDictionary)
    {
        var clonedFirstDictionary = new Dictionary<TKey, TValue>(firstDictionary);

        secondDictionary.ForEach(item => clonedFirstDictionary.TryAdd(item.Key, item.Value));

        return clonedFirstDictionary;
    }

    /// <summary>
    /// Gets the value associated with the specified key in the provided read-only dictionary.
    /// If the key is not found, the key itself is returned.
    /// </summary>
    /// <typeparam name="T">The type of keys and values in the dictionary.</typeparam>
    /// <param name="dictionary">The read-only dictionary to retrieve values from.</param>
    /// <param name="key">The key whose value to retrieve.</param>
    /// <returns>
    /// The value associated with the specified key if the key is found;
    /// otherwise, the key itself.
    /// </returns>
    /// <remarks>
    /// This method is useful for scenarios where the dictionary may not contain all keys,
    /// and returning the key itself is a valid fallback.
    /// </remarks>
    public static T GetValueOrKey<T>(this IDictionary<T, T> dictionary, T key)
    {
        return dictionary.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Get value of given key. If not found then return first value
    /// </summary>
    public static T GetValueOrFirst<T>(this IDictionary<T, T> dictionary, T key)
    {
        return dictionary.TryGetValue(key, out var value) ? value : dictionary.First().Value;
    }
}
