using System.Diagnostics.CodeAnalysis;

namespace Easy.Platform.Common.Logging.BackgroundThreadFullStackTrace;

/// <summary>
/// Used so store stack-trace for logging BEFORE Task.Run to run some new action in background. BECAUSE after call function in new background thread run, the stack trace get lost, only trace the last position to Task.Run
/// </summary>
public interface IPlatformBackgroundThreadFullStackTraceContextAccessor
{
    [AllowNull]
    string Current { get; set; }
}

public class PlatformBackgroundThreadFullStackTraceContextAccessor : IPlatformBackgroundThreadFullStackTraceContextAccessor
{
    private static readonly AsyncLocal<FullStackTraceContextHolder> FullStackTraceContextCurrent = new();

    public string Current
    {
        get => FullStackTraceContextCurrent.Value?.Context;
        set
        {
            var holder = FullStackTraceContextCurrent.Value;
            if (holder != null)
                // Clear current StackTraceContext trapped in the AsyncLocals, as its done.
                holder.Context = null;

            if (value != null)
                // Use an object indirection to hold the StackTraceContext in the AsyncLocal,
                // so it can be cleared in all ExecutionContexts when its cleared.
                FullStackTraceContextCurrent.Value = new FullStackTraceContextHolder { Context = value };
        }
    }

    private sealed class FullStackTraceContextHolder
    {
        public string Context { get; set; }
    }
}
