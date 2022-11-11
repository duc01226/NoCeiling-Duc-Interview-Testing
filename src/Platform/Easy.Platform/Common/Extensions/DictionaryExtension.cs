namespace Easy.Platform.Common.Extensions;

public static class DictionaryExtension
{
    /// <summary>
    /// Insert if item is not existed. Update if item is existed
    /// </summary>
    public static TDic Upsert<TDic, TKey, TValue>(this TDic dictionary, TKey key, TValue value) where TDic : IDictionary<TKey, TValue>
    {
        dictionary.Remove(key);
        dictionary.Add(key, value);

        return dictionary;
    }

    /// <inheritdoc cref="Upsert{TDic,TKey,TValue}"/>
    public static IDictionary<TKey, TValue> Upsert<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        dictionary.Remove(key);
        dictionary.Add(key, value);

        return dictionary;
    }
}
