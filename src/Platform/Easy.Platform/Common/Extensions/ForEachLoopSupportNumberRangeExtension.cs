namespace Easy.Platform.Common.Extensions;

/// <summary>
/// Provides extension methods to support foreach loop over number ranges.
/// </summary>
/// <remarks>
/// This class provides extension methods for the Range and int types,
/// allowing them to be used in a foreach loop. For example, you can use
/// "foreach(var i in 0..10)", "foreach(var i in ..10)", or "foreach(var i in 10)".
/// </remarks>
/// <example>
///     <code>
/// foreach(var i in 0..10)
/// {
///     Console.WriteLine(i);
/// }
/// </code>
/// </example>
public static class ForEachLoopSupportNumberRangeExtension
{
    /// <summary>
    /// Returns an enumerator that iterates over a specified range.
    /// </summary>
    /// <param name="range">The range to iterate over.</param>
    /// <returns>An enumerator that can be used to iterate over the range.</returns>
    /// <remarks>
    /// This method provides a way to iterate over a range using a foreach loop.
    /// </remarks>
    /// <example>
    ///     <code>
    /// Range range = 1..5;
    /// foreach(var i in range)
    /// {
    ///     Console.WriteLine(i);
    /// }
    /// </code>
    /// </example>
    public static Enumerator GetEnumerator(this Range range)
    {
        return new Enumerator(range);
    }

    /// <summary>
    /// Returns an enumerator that iterates up to a specified number.
    /// </summary>
    /// <param name="rangeNumber">The number to iterate up to.</param>
    /// <returns>An enumerator that can be used to iterate from 0 to the specified number.</returns>
    /// <remarks>
    /// This method provides a way to iterate from 0 to a specified number using a foreach loop.
    /// </remarks>
    /// <example>
    ///     <code>
    /// int rangeNumber = 5;
    /// foreach(var i in rangeNumber)
    /// {
    ///     Console.WriteLine(i);
    /// }
    /// </code>
    /// </example>
    public static Enumerator GetEnumerator(this int rangeNumber)
    {
        return new Enumerator(new Range(0, rangeNumber));
    }

    public ref struct Enumerator
    {
        private readonly int end;

        public Enumerator(Range range)
        {
            if (range.End.IsFromEnd) throw new NotSupportedException("Do not support infinite range like XNumber..");

            Current = range.Start.Value - 1;
            end = range.End.Value;
        }

        public int Current { get; private set; }

        public bool MoveNext()
        {
            Current++;
            return Current <= end;
        }
    }
}
