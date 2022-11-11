namespace Easy.Platform.Common.Extensions;

/// <summary>
/// Support: Ex: foreach(var i in 0..10); foreach(var i in ..10); foreach(var i in 10)
/// </summary>
public static class ForEachLoopSupportNumberRangeExtension
{
    public static Enumerator GetEnumerator(this Range range)
    {
        return new Enumerator(range);
    }

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
