namespace Easy.Platform.Common.Timing;

public interface IClockProvider
{
    DateTime Now { get; }

    DateTime UtcNow { get; }

    DateTime LocalNow { get; }

    DateTimeKind Kind { get; }

    DateTime Normalize(DateTime dateTime);
}
