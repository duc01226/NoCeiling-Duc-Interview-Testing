using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Timing;

public class LocalClockProvider : IClockProvider
{
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => DateTime.Now;

    public DateTimeKind Kind => DateTimeKind.Local;

    public DateTime Normalize(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
            return dateTime.SpecifyKind(DateTimeKind.Local);

        if (dateTime.Kind == DateTimeKind.Utc)
            return dateTime.ToLocalTime();

        return dateTime;
    }
}
