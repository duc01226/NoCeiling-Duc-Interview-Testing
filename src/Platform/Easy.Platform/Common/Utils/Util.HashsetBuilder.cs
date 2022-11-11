namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class HashsetBuilder
    {
        public static HashSet<T> New<T>(params T[] values)
        {
            return new HashSet<T>(values);
        }
    }
}
