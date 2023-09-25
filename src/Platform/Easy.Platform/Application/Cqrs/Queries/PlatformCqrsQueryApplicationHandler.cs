using System.Diagnostics;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Application.Exceptions.Extensions;
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

public abstract class PlatformCqrsQueryApplicationHandler<TQuery, TResult>
    : PlatformCqrsRequestApplicationHandler<TQuery>, IPlatformCqrsQueryApplicationHandler, IRequestHandler<TQuery, TResult>
    where TQuery : PlatformCqrsQuery<TResult>, IPlatformCqrsRequest
{
    protected readonly IPlatformCacheRepositoryProvider CacheRepositoryProvider;

    public PlatformCqrsQueryApplicationHandler(
        IPlatformApplicationUserContextAccessor userContext,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider) : base(userContext, loggerFactory, rootServiceProvider)
    {
        CacheRepositoryProvider = cacheRepositoryProvider;
        IsDistributedTracingEnabled = rootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled == true;
    }

    protected bool IsDistributedTracingEnabled { get; }

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
                            LoggerFactory.CreateLogger(typeof(PlatformCqrsQueryApplicationHandler<,>))
                                .Log(
                                    ex.IsPlatformLogicException() ? LogLevel.Warning : LogLevel.Error,
                                    ex,
                                    "[{Tag1}] Query:{RequestName} has logic error. AuditTrackId:{AuditTrackId}. Request:{Request}. UserContext:{UserContext}",
                                    ex.IsPlatformLogicException() ? "LogicErrorWarning" : "UnknownError",
                                    request.GetType().Name,
                                    request.AuditInfo.AuditTrackId,
                                    request.ToJson(),
                                    CurrentUser.GetAllKeyValues().ToJson());
                        });

                    return result;
                });
        }
        finally
        {
            Util.GarbageCollector.Collect(immediately: false);
        }
    }

    protected async Task<TResult> HandleWithTracing(TQuery request, Func<Task<TResult>> handleFunc)
    {
        if (IsDistributedTracingEnabled)
            using (var activity =
                IPlatformCqrsCommandApplicationHandler.ActivitySource.StartActivity($"QueryApplicationHandler.{nameof(Handle)}"))
            {
                activity?.SetTag("RequestType", request.GetType().Name);
                activity?.SetTag("Request", request.ToJson());

                return await handleFunc();
            }
        else return await handleFunc();
    }

    protected abstract Task<TResult> HandleAsync(TQuery request, CancellationToken cancellationToken);
}
