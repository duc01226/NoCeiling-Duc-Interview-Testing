using System.IO;
using System.Reflection;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class PathBuilder
    {
        public static string ConcatRelativePath(params string[] paths)
        {
            return paths.Aggregate((current, next) => current.TrimEnd('/') + "/" + next.TrimStart('/'));
        }

        /// <summary>
        /// Get full path from Relative path to entry executing assembly location
        /// </summary>
        public static string GetFullPathByRelativeToEntryExecutionPath(string relativePath)
        {
            return Path.GetFullPath(relativePath, Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!);
        }
    }
}
