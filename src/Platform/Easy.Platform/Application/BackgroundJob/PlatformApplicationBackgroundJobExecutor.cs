using System.Diagnostics;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.BackgroundJob;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.BackgroundJob;

public interface IPlatformApplicationBackgroundJobExecutor
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformApplicationBackgroundJobExecutor)}");
}

public abstract class PlatformApplicationBackgroundJobExecutor<TParam> : PlatformBackgroundJobExecutor<TParam>, IPlatformApplicationBackgroundJobExecutor
    where TParam : class
{
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformApplicationBackgroundJobExecutor(
        IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerBuilder) : base(loggerBuilder)
    {
        UnitOfWorkManager = unitOfWorkManager;
    }

    public virtual bool AutoOpenUow => true;

    protected override async Task InternalExecuteAsync(TParam param = null)
    {
        using (var activity = IPlatformApplicationBackgroundJobExecutor.ActivitySource.StartActivity())
        {
            activity?.SetTag("Type", GetType().Name);
            activity?.SetTag("Param", param?.ToJson());

            if (AutoOpenUow)
                using (var uow = UnitOfWorkManager.Begin())
                {
                    await ProcessAsync(param);

                    await uow.CompleteAsync();
                }
            else
                await ProcessAsync(param);
        }
    }
}

public abstract class PlatformApplicationBackgroundJobExecutor : PlatformApplicationBackgroundJobExecutor<object>
{
    protected PlatformApplicationBackgroundJobExecutor(
        IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerBuilder) : base(unitOfWorkManager, loggerBuilder)
    {
    }
}
