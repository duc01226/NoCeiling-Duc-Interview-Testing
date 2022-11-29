using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.BackgroundJob;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.BackgroundJob;

public abstract class PlatformApplicationBackgroundJobExecutor<TParam> : PlatformBackgroundJobExecutor<TParam>
    where TParam : class
{
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformApplicationBackgroundJobExecutor(
        IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        UnitOfWorkManager = unitOfWorkManager;
    }

    public virtual bool AutoOpenUow => true;

    protected override async Task InternalExecuteAsync(TParam param)
    {
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

public abstract class PlatformApplicationBackgroundJobExecutor : PlatformApplicationBackgroundJobExecutor<object>
{
    protected PlatformApplicationBackgroundJobExecutor(
        IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory) : base(unitOfWorkManager, loggerFactory)
    {
    }
}
