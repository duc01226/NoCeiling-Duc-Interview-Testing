using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.AspNetCore.Constants;
using Easy.Platform.AspNetCore.Middleware.Abstracts;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.AspNetCore.Middleware;

/// <summary>
/// This middleware will add a generated guid request id in to headers. It should be added at the first middleware or second after UseGlobalExceptionHandlerMiddleware
/// </summary>
public class PlatformRequestIdGeneratorMiddleware : PlatformMiddleware
{
    private readonly IPlatformApplicationUserContextAccessor applicationUserContextAccessor;

    public PlatformRequestIdGeneratorMiddleware(
        RequestDelegate next,
        IPlatformApplicationUserContextAccessor applicationUserContextAccessor) : base(next)
    {
        this.applicationUserContextAccessor = applicationUserContextAccessor;
    }

    protected override async Task InternalInvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(
                PlatformAspnetConstant.CommonHttpHeaderNames.RequestId,
                out var existedRequestId) ||
            string.IsNullOrEmpty(existedRequestId))
            context.Request.Headers.Upsert(
                PlatformAspnetConstant.CommonHttpHeaderNames.RequestId,
                Guid.NewGuid().ToString());

        context.TraceIdentifier = context.Request.Headers[PlatformAspnetConstant.CommonHttpHeaderNames.RequestId];
        applicationUserContextAccessor.Current.SetValue(
            context.TraceIdentifier,
            PlatformApplicationCommonUserContextKeys.RequestIdContextKey);

        // Add the request ID to the response header for client side tracking
        context.Response.OnStarting(
            () =>
            {
                if (!context.Response.Headers.ContainsKey(PlatformAspnetConstant.CommonHttpHeaderNames.RequestId))
                    context.Response.Headers.Add(
                        PlatformAspnetConstant.CommonHttpHeaderNames.RequestId,
                        Util.ListBuilder.NewArray(context.TraceIdentifier));
                return Task.CompletedTask;
            });

        // Call the next delegate/middleware in the pipeline
        await Next(context);
    }
}
