using System.Diagnostics;
using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Application.Exceptions.Extensions;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Queries;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Extensions;
using Easy.Platform.Infrastructures.Caching;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Queries;

public interface IPlatformCqrsQueryApplicationHandler
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformCqrsQueryApplicationHandler)}");
}

/// <summary>
/// Provides a base class for application-level handlers of CQRS query requests with a specified result type.
/// </summary>
/// <typeparam name="TQuery">The type of CQRS query handled by this class.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the query handler.</typeparam>
public abstract class PlatformCqrsQueryApplicationHandler<TQuery, TResult>
    : PlatformCqrsRequestApplicationHandler<TQuery>, IPlatformCqrsQueryApplicationHandler, IRequestHandler<TQuery, TResult>
    where TQuery : PlatformCqrsQuery<TResult>, IPlatformCqrsRequest
{
    /// <summary>
    /// The cache repository provider for handling caching operations.
    /// </summary>
    protected readonly IPlatformCacheRepositoryProvider CacheRepositoryProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformCqrsQueryApplicationHandler{TQuery, TResult}" /> class.
    /// </summary>
    /// <param name="requestContextAccessor">The request context accessor providing information about the current application request context.</param>
    /// <param name="loggerFactory">The logger factory used for creating loggers.</param>
    /// <param name="rootServiceProvider">The root service provider for resolving dependencies.</param>
    /// <param name="cacheRepositoryProvider">The cache repository provider for handling caching operations.</param>
    protected PlatformCqrsQueryApplicationHandler(
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider)
        : base(requestContextAccessor, loggerFactory, rootServiceProvider)
    {
        CacheRepositoryProvider = cacheRepositoryProvider ?? throw new ArgumentNullException(nameof(cacheRepositoryProvider));
        IsDistributedTracingEnabled = rootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled == true;
        ApplicationSettingContext = rootServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();
    }

    /// <summary>
    /// Gets a value indicating whether distributed tracing is enabled.
    /// </summary>
    protected bool IsDistributedTracingEnabled { get; }

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    /// <summary>
    /// Handles the specified CQRS query asynchronously.
    /// </summary>
    /// <param name="request">The CQRS query to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The result of handling the CQRS query.</returns>
    public async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await HandleWithTracing(
                request,
                async () =>
                {
                    request.SetAuditInfo<TQuery>(BuildRequestAuditInfo(request));

                    await ValidateRequestAsync(request.Validate().Of<TQuery>(), cancellationToken).EnsureValidAsync();

                    var result = await Util.TaskRunner.CatchExceptionContinueThrowAsync(
                        () => HandleAsync(request, cancellationToken),
                        onException: ex =>
                        {
                            LoggerFactory.CreateLogger(typeof(PlatformCqrsQueryApplicationHandler<,>).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}")
                                .Log(
                                    ex.IsPlatformLogicException() ? LogLevel.Warning : LogLevel.Error,
                                    ex,
                                    "[{Tag1}] Query:{RequestName} has logic error. AuditTrackId:{AuditTrackId}. Request:{Request}. UserContext:{UserContext}",
                                    ex.IsPlatformLogicException() ? "LogicErrorWarning" : "UnknownError",
                                    request.GetType().Name,
                                    request.AuditInfo?.AuditTrackId,
                                    request.ToFormattedJson(),
                                    RequestContext.GetAllKeyValues(ApplicationSettingContext.GetIgnoreRequestContextKeys()).ToFormattedJson());
                        });

                    return result;
                });
        }
        finally
        {
            if (ApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessage)
                Util.GarbageCollector.Collect(ApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds);
        }
    }

    /// <summary>
    /// Handles the specified CQRS query with distributed tracing.
    /// </summary>
    /// <param name="request">The CQRS query to handle.</param>
    /// <param name="handleFunc">The function representing the handling logic.</param>
    /// <returns>The result of handling the CQRS query.</returns>
    protected async Task<TResult> HandleWithTracing(TQuery request, Func<Task<TResult>> handleFunc)
    {
        if (IsDistributedTracingEnabled)
            using (var activity =
                IPlatformCqrsCommandApplicationHandler.ActivitySource.StartActivity($"QueryApplicationHandler.{nameof(Handle)}"))
            {
                activity?.SetTag("RequestType", request.GetType().Name);
                activity?.SetTag("Request", request.ToFormattedJson());

                return await handleFunc();
            }

        return await handleFunc();
    }

    /// <summary>
    /// Handles the specified CQRS query asynchronously.
    /// </summary>
    /// <param name="request">The CQRS query to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The result of handling the CQRS query.</returns>
    protected abstract Task<TResult> HandleAsync(TQuery request, CancellationToken cancellationToken);
}
