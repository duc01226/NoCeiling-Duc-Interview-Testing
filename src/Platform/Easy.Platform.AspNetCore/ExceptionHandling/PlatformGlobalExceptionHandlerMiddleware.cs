using System.Net;
using System.Text.Json;
using Easy.Platform.Application;
using Easy.Platform.Application.Exceptions;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.AspNetCore.Middleware.Abstracts;
using Easy.Platform.Common;
using Easy.Platform.Common.Exceptions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.AspNetCore.ExceptionHandling;

/// <summary>
/// This middleware should be used it at the first level to catch general exception from any next middleware.
/// </summary>
public class PlatformGlobalExceptionHandlerMiddleware : PlatformMiddleware
{
    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private readonly Lazy<ILogger> loggerLazy;

    public PlatformGlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(next)
    {
        loggerLazy = new Lazy<ILogger>(() => loggerFactory.CreateLogger<PlatformGlobalExceptionHandlerMiddleware>());
        RequestContextAccessor = requestContextAccessor;
        this.applicationSettingContext = applicationSettingContext;
        Configuration = configuration;
    }

    protected IConfiguration Configuration { get; }
    protected ILogger Logger => loggerLazy.Value;
    protected IPlatformApplicationRequestContextAccessor RequestContextAccessor { get; }
    protected bool DeveloperExceptionEnabled => PlatformEnvironment.IsDevelopment || Configuration.GetValue<bool>("DeveloperExceptionEnabled");

    protected override async Task InternalInvokeAsync(HttpContext context)
    {
        try
        {
            await Next(context);
        }
        catch (Exception e)
        {
            try
            {
                await OnException(context, e);
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException or TaskCanceledException)
                    Logger.LogWarning(exception.BeautifyStackTrace(), "Exception {Exception}", exception.GetType().Name);
                else
                    Logger.LogError(exception.BeautifyStackTrace(), "Exception {Exception}", exception.GetType().Name);
            }
        }
    }

    protected virtual Task OnException(HttpContext context, Exception exception)
    {
        if (exception is BadHttpRequestException or OperationCanceledException or TaskCanceledException)
        {
            Logger.LogWarning(exception, "Exception {Exception}", exception.GetType().Name);
            return Task.CompletedTask;
        }

        var errorResponse = exception
            .WhenIs<Exception, PlatformPermissionException, PlatformAspNetMvcErrorResponse>(
                permissionException => new PlatformAspNetMvcErrorResponse(
                    PlatformAspNetMvcErrorInfo.FromPermissionException(permissionException, DeveloperExceptionEnabled),
                    HttpStatusCode.Forbidden,
                    context.TraceIdentifier).PipeAction(_ => LogKnownRequestWarning(exception, context)))
            .WhenIs<IPlatformValidationException>(
                validationException => new PlatformAspNetMvcErrorResponse(
                    PlatformAspNetMvcErrorInfo.FromValidationException(validationException, DeveloperExceptionEnabled),
                    HttpStatusCode.BadRequest,
                    context.TraceIdentifier).PipeAction(_ => LogKnownRequestWarning(exception, context)))
            .WhenIs<PlatformApplicationException>(
                applicationException => new PlatformAspNetMvcErrorResponse(
                    PlatformAspNetMvcErrorInfo.FromApplicationException(applicationException, DeveloperExceptionEnabled),
                    HttpStatusCode.BadRequest,
                    context.TraceIdentifier).PipeAction(_ => LogKnownRequestWarning(exception, context)))
            .WhenIs<PlatformNotFoundException>(
                domainNotFoundException => new PlatformAspNetMvcErrorResponse(
                    PlatformAspNetMvcErrorInfo.FromNotFoundException(domainNotFoundException, DeveloperExceptionEnabled),
                    HttpStatusCode.NotFound,
                    context.TraceIdentifier).PipeAction(_ => LogKnownRequestWarning(exception, context)))
            .WhenIs<PlatformDomainException>(
                domainException => new PlatformAspNetMvcErrorResponse(
                    PlatformAspNetMvcErrorInfo.FromDomainException(domainException, DeveloperExceptionEnabled),
                    HttpStatusCode.BadRequest,
                    context.TraceIdentifier).PipeAction(_ => LogKnownRequestWarning(exception, context)))
            .Else(
                exception =>
                {
                    Logger.LogError(
                        exception.BeautifyStackTrace(),
                        "[UnexpectedRequestError] There is an unexpected exception during the processing of the request. RequestId: {RequestId}. RequestContext: {RequestContext}",
                        context.TraceIdentifier,
                        RequestContextAccessor.Current.GetAllKeyValues(applicationSettingContext.GetIgnoreRequestContextKeys()).ToFormattedJson());

                    return new PlatformAspNetMvcErrorResponse(
                        PlatformAspNetMvcErrorInfo.FromUnknownException(exception, DeveloperExceptionEnabled),
                        HttpStatusCode.InternalServerError,
                        context.TraceIdentifier);
                })
            .Execute();

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = errorResponse.StatusCode;
        return context.Response.WriteAsync(
            PlatformJsonSerializer.Serialize(errorResponse, options => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase),
            context.RequestAborted);
    }

    protected void LogKnownRequestWarning(Exception exception, HttpContext context)
    {
        Logger.LogWarning(
            exception,
            "[KnownRequestWarning] There is a {ExceptionType} during the processing of the request. RequestId: {RequestId}",
            exception.GetType(),
            context.TraceIdentifier);
    }
}
