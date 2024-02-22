namespace Easy.Platform.Application.RequestContext;

/// <summary>
/// Implementation of <see cref="IPlatformApplicationRequestContextAccessor" />
/// Inspired by Microsoft.AspNetCore.Http.HttpContextAccessor
/// </summary>
public class PlatformDefaultApplicationRequestContextAccessor : IPlatformApplicationRequestContextAccessor
{
    private static readonly AsyncLocal<UserContextHolder> UserContextCurrentThread = new();

    public IPlatformApplicationRequestContext Current
    {
        get
        {
            if (UserContextCurrentThread.Value == null)
                Current = CreateNewContext();

            return UserContextCurrentThread.Value?.Context;
        }
        set
        {
            var holder = UserContextCurrentThread.Value;
            if (holder != null)
                // WHY: Clear current Context trapped in the AsyncLocals, as its done using
                // because we want to set a new current user context.
                holder.Context = null;

            if (value != null)
                // WHY: Use an object indirection to hold the Context in the AsyncLocal,
                // so it can be cleared in all ExecutionContexts when its cleared.
                UserContextCurrentThread.Value = new UserContextHolder
                {
                    Context = value
                };
        }
    }

    protected virtual IPlatformApplicationRequestContext CreateNewContext()
    {
        return new PlatformDefaultApplicationRequestContext();
    }

    protected sealed class UserContextHolder
    {
        public IPlatformApplicationRequestContext Context { get; set; }
    }
}
