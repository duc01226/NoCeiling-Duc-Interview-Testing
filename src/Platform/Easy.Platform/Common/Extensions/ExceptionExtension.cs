#nullable enable
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
                InnerException = includeInnerException ? exception.InnerException?.Pipe(_ => Serialize(_, includeInnerException)) : null,
                exception.StackTrace
            });
    }
}
