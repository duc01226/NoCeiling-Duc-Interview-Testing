namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class DictionaryBuilder
    {
        public static Dictionary<TKey, TValue> New<TKey, TValue>(params ValueTuple<TKey, TValue>[] items)
        {
            return new Dictionary<TKey, TValue>(items.Select(p => new KeyValuePair<TKey, TValue>(p.Item1, p.Item2)));
        }
    }
}
