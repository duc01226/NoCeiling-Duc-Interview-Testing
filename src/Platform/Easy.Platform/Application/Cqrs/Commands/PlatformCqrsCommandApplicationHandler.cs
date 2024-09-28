using System.Diagnostics;
using Easy.Platform.Application.Exceptions.Extensions;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Commands;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Cqrs.Commands;

public interface IPlatformCqrsCommandApplicationHandler
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformCqrsCommandApplicationHandler)}");
}

/// <summary>
/// Provides a base class for application-level handlers of CQRS command requests with a specified result type.
/// </summary>
/// <typeparam name="TCommand">The type of CQRS command handled by this class.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the command handler.</typeparam>
public abstract class PlatformCqrsCommandApplicationHandler<TCommand, TResult> : PlatformCqrsRequestApplicationHandler<TCommand>, IRequestHandler<TCommand, TResult>
    where TCommand : PlatformCqrsCommand<TResult>, IPlatformCqrsRequest, new()
    where TResult : PlatformCqrsCommandResult, new()
{
    /// <summary>
    /// The CQRS service for handling commands.
    /// </summary>
    protected readonly Lazy<IPlatformCqrs> Cqrs;

    /// <summary>
    /// The unit of work manager for managing database transactions.
    /// </summary>
    protected readonly IPlatformUnitOfWorkManager UnitOfWorkManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformCqrsCommandApplicationHandler{TCommand, TResult}" /> class.
    /// </summary>
    /// <param name="requestContextAccessor">The request context accessor providing information about the current application request context.</param>
    /// <param name="unitOfWorkManager">The unit of work manager for managing database transactions.</param>
    /// <param name="cqrs">The CQRS service for handling commands.</param>
    /// <param name="loggerFactory">The logger factory used for creating loggers.</param>
    /// <param name="rootServiceProvider">The root service provider for resolving dependencies.</param>
    protected PlatformCqrsCommandApplicationHandler(
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        Lazy<IPlatformCqrs> cqrs,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider)
        : base(requestContextAccessor, loggerFactory, rootServiceProvider)
    {
        UnitOfWorkManager = unitOfWorkManager;
        Cqrs = cqrs;
        IsDistributedTracingEnabled = rootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled == true;
        ApplicationSettingContext = rootServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();
    }

    /// <summary>
    /// Gets a value indicating whether distributed tracing is enabled.
    /// </summary>
    protected bool IsDistributedTracingEnabled { get; }

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    /// <summary>
    /// Gets the number of retry attempts after a failure.
    /// </summary>
    public virtual int RetryOnFailedTimes { get; set; } = 2;

    public virtual double RetryOnFailedDelaySeconds { get; set; } = 0.5;

    /// <summary>
    /// Gets a value indicating whether a unit of work should be automatically opened.
    /// </summary>
    protected virtual bool AutoOpenUow => true;

    /// <summary>
    /// Handles the specified CQRS command asynchronously.
    /// </summary>
    /// <param name="request">The CQRS command to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The result of handling the CQRS command.</returns>
    public virtual async Task<TResult> Handle(TCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await HandleWithTracing(
                request,
                async () =>
                {
                    await ValidateRequestAsync(request, cancellationToken).EnsureValidAsync();

                    var result = await Util.TaskRunner.CatchExceptionContinueThrowAsync(
                        () => ExecuteHandleAsync(request, cancellationToken),
                        onException: ex =>
                        {
                            if (!ex.IsPlatformLogicException())
                                LoggerFactory.CreateLogger(typeof(PlatformCqrsCommandApplicationHandler<>).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}")
                                    .LogError(
                                        ex.BeautifyStackTrace(),
                                        "[{Tag1}] Command:{RequestName} has error {Error}. AuditTrackId:{AuditTrackId}. Request:{Request}. RequestContext:{RequestContext}",
                                        "UnknownError",
                                        request.GetType().Name,
                                        ex.Message,
                                        request.AuditInfo?.AuditTrackId,
                                        request.ToJson(),
                                        RequestContext.GetAllKeyValues(ApplicationSettingContext.GetIgnoreRequestContextKeys()).ToJson());
                            else
                                LoggerFactory.CreateLogger(typeof(PlatformCqrsCommandApplicationHandler<>).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}")
                                    .LogWarning(
                                        "[{Tag1}] Command:{RequestName} has error {Error}. AuditTrackId:{AuditTrackId}. Request:{Request}.",
                                        "LogicErrorWarning",
                                        request.GetType().Name,
                                        ex.Message,
                                        request.AuditInfo?.AuditTrackId,
                                        request.ToJson());
                        });

                    if (RootServiceProvider.IsAnyImplementationAssignableToServiceTypeRegistered(
                        typeof(IPlatformCqrsEventHandler<PlatformCqrsCommandEvent<TCommand, TResult>>)))
                        await Cqrs.Value.SendEvent(
                            new PlatformCqrsCommandEvent<TCommand, TResult>(request, result, PlatformCqrsCommandEventAction.Executed)
                                .With(p => p.SetRequestContextValues(RequestContext.GetAllKeyValues(ApplicationSettingContext.GetIgnoreRequestContextKeys()))),
                            cancellationToken);

                    return result;
                });
        }
        finally
        {
            if (ApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessage)
                await Util.GarbageCollector.Collect(ApplicationSettingContext.AutoGarbageCollectPerProcessRequestOrBusMessageThrottleTimeSeconds);
        }
    }

    /// <summary>
    /// Handles the specified CQRS command with distributed tracing.
    /// </summary>
    /// <param name="request">The CQRS command to handle.</param>
    /// <param name="handleFunc">The function representing the handling logic.</param>
    /// <returns>The result of handling the CQRS command.</returns>
    protected async Task<TResult> HandleWithTracing(TCommand request, Func<Task<TResult>> handleFunc)
    {
        if (IsDistributedTracingEnabled)
            using (var activity =
                IPlatformCqrsCommandApplicationHandler.ActivitySource.StartActivity($"CommandApplicationHandler.{nameof(Handle)}"))
            {
                activity?.SetTag("RequestType", request.GetType().Name);
                activity?.SetTag("Request", request.ToFormattedJson());

                return await handleFunc();
            }

        return await handleFunc();
    }

    /// <summary>
    /// Handles the specified CQRS command asynchronously with retry logic and unit of work management.
    /// </summary>
    /// <param name="request">The CQRS command to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The result of handling the CQRS command.</returns>
    protected abstract Task<TResult> HandleAsync(TCommand request, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the handling logic for the CQRS command with retry logic.
    /// </summary>
    /// <param name="request">The CQRS command to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The result of handling the CQRS command.</returns>
    protected virtual async Task<TResult> ExecuteHandleAsync(TCommand request, CancellationToken cancellationToken)
    {
        if (RetryOnFailedTimes > 0)
            return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => DoExecuteHandleAsync(request, cancellationToken),
                retryCount: RetryOnFailedTimes,
                sleepDurationProvider: i => RetryOnFailedDelaySeconds.Seconds(),
                cancellationToken: cancellationToken);
        return await DoExecuteHandleAsync(request, cancellationToken);
    }

    /// <summary>
    /// Executes the handling logic for the CQRS command with or without opening a unit of work.
    /// </summary>
    /// <param name="request">The CQRS command to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The result of handling the CQRS command.</returns>
    protected virtual async Task<TResult> DoExecuteHandleAsync(TCommand request, CancellationToken cancellationToken)
    {
        if (AutoOpenUow == false) return await HandleAsync(request, cancellationToken);

        using (var uow = UnitOfWorkManager.Begin())
        {
            var result = await HandleAsync(request, cancellationToken);

            await uow.CompleteAsync(cancellationToken);

            return result;
        }
    }
}

/// <summary>
/// Provides a base class for application-level handlers of CQRS command requests with a default result type.
/// </summary>
/// <typeparam name="TCommand">The type of CQRS command handled by this class.</typeparam>
public abstract class PlatformCqrsCommandApplicationHandler<TCommand> : PlatformCqrsCommandApplicationHandler<TCommand, PlatformCqrsCommandResult>
    where TCommand : PlatformCqrsCommand<PlatformCqrsCommandResult>, IPlatformCqrsRequest, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformCqrsCommandApplicationHandler{TCommand}" /> class.
    /// </summary>
    /// <param name="requestContextAccessor">The request context accessor providing information about the current application request context.</param>
    /// <param name="unitOfWorkManager">The unit of work manager for managing database transactions.</param>
    /// <param name="cqrs">The CQRS service for handling commands.</param>
    /// <param name="loggerFactory">The logger factory used for creating loggers.</param>
    /// <param name="rootServiceProvider">The root service provider for resolving dependencies.</param>
    protected PlatformCqrsCommandApplicationHandler(
        IPlatformApplicationRequestContextAccessor requestContextAccessor,
        IPlatformUnitOfWorkManager unitOfWorkManager,
        Lazy<IPlatformCqrs> cqrs,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider)
        : base(requestContextAccessor, unitOfWorkManager, cqrs, loggerFactory, rootServiceProvider)
    {
    }

    /// <summary>
    /// Handles the specified CQRS command without a result.
    /// </summary>
    /// <param name="request">The CQRS command to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public abstract Task HandleNoResult(TCommand request, CancellationToken cancellationToken);

    /// <summary>
    /// Handles the specified CQRS command with a default result.
    /// </summary>
    /// <param name="request">The CQRS command to handle.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>The result of handling the CQRS command.</returns>
    protected override async Task<PlatformCqrsCommandResult> HandleAsync(
        TCommand request,
        CancellationToken cancellationToken)
    {
        await HandleNoResult(request, cancellationToken);
        return new PlatformCqrsCommandResult();
    }
}
