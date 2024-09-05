#nullable enable
using System.Diagnostics;
using Easy.Platform.Common.JsonSerialization;

namespace Easy.Platform.Common.Extensions;

public static class ExceptionExtension
{
    public static string Serialize(this Exception exception, bool includeInnerException = true)
    {
        return PlatformJsonSerializer.Serialize(
            new
            {
                exception.Message,
                InnerException = includeInnerException ? exception.InnerException?.Pipe(ex => Serialize(ex, includeInnerException)) : null,
                exception.StackTrace
            });
    }

    public static Exception BeautifyStackTrace(this Exception exception)
    {
        return exception.Demystify();
    }
}
