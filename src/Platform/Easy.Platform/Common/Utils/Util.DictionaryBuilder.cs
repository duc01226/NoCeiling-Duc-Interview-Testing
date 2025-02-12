namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class DictionaryBuilder
    {
        public static Dictionary<TKey, TValue> New<TKey, TValue>(params ValueTuple<TKey, TValue>[] items)
        {
            return items.Select(p => new KeyValuePair<TKey, TValue>(p.Item1, p.Item2)).ToDictionary(p => p.Key, p => p.Value);
        }
    }
}
