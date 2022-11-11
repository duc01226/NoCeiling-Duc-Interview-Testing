namespace Easy.Platform.Common.Timing;

public interface IClockProvider
{
    DateTime Now { get; }

    DateTimeKind Kind { get; }

    DateTime Normalize(DateTime dateTime);
}
