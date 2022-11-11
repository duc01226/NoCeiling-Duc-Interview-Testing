namespace Easy.Platform.Common.Utils;

/// <summary>
/// Utils is class to store all static small functions which could be used in any project.
/// This do not have any logic related to any domains.
/// Utils default grouping by "output", either by the output data type, or serve a "functional purpose".
/// Example: Utils.String should produce string as output.Utils.Enums should produce enum as output.Utils.Copy should only
/// do the copy data functional.
/// </summary>
public static partial class Util
{
    public static T CreateInstance<T>(params object[] paramArray)
    {
        return (T)Activator.CreateInstance(typeof(T), args: paramArray);
    }
}
