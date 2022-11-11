namespace Easy.Platform.Common.DeprecatedFPLibrary;

public static partial class F
{
    public static Option.None None => Option.None.Default; // the None value

    public static ValueTuple Unit()
    {
        return default;
    }

    public static Option<T> Some<T>(T value)
    {
        return new Option.Some<T>(value); // wrap the given value into a Some
    }

    public static List<T> List<T>(params T[] items)
    {
        return new List<T>(items);
    }
}
