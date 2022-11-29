namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class Path
    {
        public static string ConcatRelativePath(params string[] paths)
        {
            return paths.Aggregate((current, next) => current.TrimEnd('/') + "/" + next.TrimStart('/'));
        }
    }
}
