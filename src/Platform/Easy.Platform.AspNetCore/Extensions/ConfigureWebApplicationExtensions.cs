using System.IO;
using Easy.Platform.AspNetCore.ExceptionHandling;
using Easy.Platform.AspNetCore.Middleware;
using Easy.Platform.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
        var defaultCorsPolicyName = applicationBuilder.ApplicationServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment() || PlatformEnvironment.IsDevelopment
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

public static class WebHostBuilderExtensions
{
    /// <summary>
    /// Use the given https certificate for handling and trust https request
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <param name="httpsCertFileRelativePath">Relative path to entry executing assembly location</param>
    /// <param name="httpsCertPassword"></param>
    /// <param name="ignoreIfFileNotExisting"></param>
    /// <returns></returns>
    public static IWebHostBuilder UseCustomHttpsCert(
        this IWebHostBuilder hostBuilder,
        string httpsCertFileRelativePath,
        string httpsCertPassword,
        bool ignoreIfFileNotExisting = false)
    {
        var fullHttpsCertFilePath = Util.PathBuilder.GetFullPathByRelativeToEntryExecutionPath(httpsCertFileRelativePath);

        var isCertFileExisting = File.Exists(fullHttpsCertFilePath)
            .Ensure(
                must: isCertFileExisting => isCertFileExisting || ignoreIfFileNotExisting,
                $"HttpsCertFileRelativePath:[{httpsCertFileRelativePath}] to FullHttpsCertFilePath:[{fullHttpsCertFilePath}] does not exists");
        var listenUrls = PlatformEnvironment.AspCoreUrlsValue?.Split(";");

        return hostBuilder.PipeIf(
            listenUrls != null && isCertFileExisting,
            p => p.ConfigureKestrel(
                serverOptions =>
                {
                    listenUrls!.ForEach(
                        listenUrl =>
                        {
                            if (listenUrl.StartsWith("http://*:") || listenUrl.StartsWith("https://*:"))
                            {
                                var listenAnyPort = listenUrl
                                    .Replace("http://*:", "http://0.0.0.0:")
                                    .Replace("https://*:", "https://0.0.0.0:")
                                    .ToUri()
                                    .Port;

                                serverOptions.ListenAnyIP(
                                    listenAnyPort,
                                    listenOptions => ConfigUseHttps(listenOptions, listenUrl, fullHttpsCertFilePath!, httpsCertPassword));
                            }
                            else if (listenUrl.Contains("://localhost"))
                            {
                                serverOptions.ListenLocalhost(
                                    listenUrl.ToUri().Port,
                                    listenOptions => ConfigUseHttps(listenOptions, listenUrl, fullHttpsCertFilePath!, httpsCertPassword));
                            }
                            else
                            {
                                serverOptions.Listen(
                                    new UriEndPoint(listenUrl.ToUri()),
                                    listenOptions => ConfigUseHttps(listenOptions, listenUrl, fullHttpsCertFilePath!, httpsCertPassword));
                            }
                        });
                }));

        static void ConfigUseHttps(ListenOptions listenOptions, string listenUrl, string certFilePath, string certPassword)
        {
            listenOptions.PipeIf(listenUrl.StartsWith("https"), _ => _.UseHttps(fileName: certFilePath!, certPassword));
        }
    }
}
