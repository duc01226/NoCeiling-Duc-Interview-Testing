using Easy.Platform.Application.RequestContext;
using Easy.Platform.AspNetCore.Constants;
using Easy.Platform.AspNetCore.Middleware.Abstracts;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.AspNetCore.Middleware;

/// <summary>
/// Middleware for generating a request ID and adding it to headers. Should be added at the first middleware
/// or second after UseGlobalExceptionHandlerMiddleware.
/// </summary>
/// <remarks>
/// This middleware will add a generated guid request id in to headers. It should be added at the first middleware or second after UseGlobalExceptionHandlerMiddleware
/// </remarks>
public class PlatformRequestIdGeneratorMiddleware : PlatformMiddleware
{
    private readonly IPlatformApplicationRequestContextAccessor applicationUserContextAccessor;

    public PlatformRequestIdGeneratorMiddleware(
        RequestDelegate next,
        IPlatformApplicationRequestContextAccessor applicationUserContextAccessor) : base(next)
    {
        this.applicationUserContextAccessor = applicationUserContextAccessor;
    }

    protected override async Task InternalInvokeAsync(HttpContext context)
    {
        // Generate a new request ID if not present in the headers
        if (!context.Request.Headers.TryGetValue(
                PlatformAspnetConstant.CommonHttpHeaderNames.RequestId,
                out var existedRequestId) ||
            string.IsNullOrEmpty(existedRequestId))
            context.Request.Headers.Upsert(
                PlatformAspnetConstant.CommonHttpHeaderNames.RequestId,
                Ulid.NewUlid().ToString());

        // Set the trace identifier for the context
        context.TraceIdentifier = context.Request.Headers[PlatformAspnetConstant.CommonHttpHeaderNames.RequestId];
        applicationUserContextAccessor.Current.SetValue(
            context.TraceIdentifier,
            PlatformApplicationCommonRequestContextKeys.RequestIdContextKey);

        // Add the request ID to the response header for client-side tracking
        context.Response.OnStarting(
            () =>
            {
                if (!context.Response.Headers.ContainsKey(PlatformAspnetConstant.CommonHttpHeaderNames.RequestId))
                    context.Response.Headers.Append(
                        PlatformAspnetConstant.CommonHttpHeaderNames.RequestId,
                        Util.ListBuilder.NewArray(context.TraceIdentifier));
                return Task.CompletedTask;
            });

        // Call the next delegate/middleware in the pipeline
        await Next(context);
    }
}
