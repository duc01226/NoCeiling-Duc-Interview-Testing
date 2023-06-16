using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Timing;

public class UtcClockProvider : IClockProvider
{
    public DateTime Now => DateTime.UtcNow;
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => DateTime.Now;

    public DateTimeKind Kind => DateTimeKind.Utc;

    public DateTime Normalize(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
            return dateTime.SpecifyKind(DateTimeKind.Utc);

        if (dateTime.Kind == DateTimeKind.Local)
            return dateTime.ToUtc();

        return dateTime;
    }
}
