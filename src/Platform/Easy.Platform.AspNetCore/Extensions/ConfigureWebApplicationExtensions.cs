using Easy.Platform.AspNetCore.ExceptionHandling;
using Easy.Platform.AspNetCore.Middleware;
using Easy.Platform.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Easy.Platform.AspNetCore.Extensions;

public static class ConfigureWebApplicationExtensions
{
    /// <summary>
    /// This middleware will add a generated guid request id in to headers. It should be added at the first middleware or
    /// second after UseGlobalExceptionHandlerMiddleware
    /// </summary>
    public static IApplicationBuilder UseRequestIdGeneratorMiddleware(this IApplicationBuilder applicationBuilder)
    {
        return applicationBuilder.UseMiddleware<PlatformRequestIdGeneratorMiddleware>();
    }

    /// <summary>
    /// This middleware should be used it at the first level to catch exception from any next middleware.
    /// <see cref="PlatformGlobalExceptionHandlerMiddleware" /> will be used.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandlerMiddleware(this IApplicationBuilder applicationBuilder)
    {
        return applicationBuilder.UseMiddleware<PlatformGlobalExceptionHandlerMiddleware>();
    }

    /// <summary>
    /// With endpoint routing, the CORS middleware must be configured to execute between the calls to UseRouting and
    /// UseEndpoints.
    /// Incorrect configuration will cause the middleware to stop functioning correctly.
    /// Use <see cref="PlatformAspNetCoreModuleDefaultPolicies.DevelopmentCorsPolicy" /> in dev environment,
    /// if not then <see cref="PlatformAspNetCoreModuleDefaultPolicies.CorsPolicy" /> will be used
    /// </summary>
    public static IApplicationBuilder UseDefaultCorsPolicy(
        this IApplicationBuilder applicationBuilder,
        string specificCorPolicy = null)
    {
        var defaultCorsPolicyName = applicationBuilder.ApplicationServices.GetService<IWebHostEnvironment>().IsDevelopment() || PlatformEnvironment.IsDevelopment
            ? PlatformAspNetCoreModuleDefaultPolicies.DevelopmentCorsPolicy
            : PlatformAspNetCoreModuleDefaultPolicies.CorsPolicy;
        applicationBuilder.UseCors(specificCorPolicy ?? defaultCorsPolicyName);

        return applicationBuilder;
    }

    /// <summary>
    /// If the request is not handled by any Endpoints Controllers, The request will come to this middleware.<br />
    /// If the request path is empty default, return "Service is up" for health check that this api service is online.<br />
    /// This should be placed after UseEndpoints or MapControllers
    /// </summary>
    public static void UseDefaultResponseHealthCheckForEmptyRootPath(this IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Run(
            async context =>
            {
                if (context.Request.Path == "/")
                    await context.Response.WriteAsync("Service is up.");
            });
    }
}
