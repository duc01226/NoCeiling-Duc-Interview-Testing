using Easy.Platform.AspNetCore.Middleware.Abstracts;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.AspNetCore.Middleware;

public class PlatformMemoryGarbageCollectorAfterRequestMiddleware : PlatformMiddleware
{
    public PlatformMemoryGarbageCollectorAfterRequestMiddleware(RequestDelegate next) : base(next)
    {
    }

    protected override async Task InternalInvokeAsync(HttpContext context)
    {
        try
        {
            await Next(context);
        }
        finally
        {
            Util.GarbageCollector.Collect();
        }
    }
}
