using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.BackgroundJob;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.BackgroundJob;

public abstract class PlatformApplicationBackgroundJobExecutor : PlatformBackgroundJobExecutor
{
    protected readonly ILogger Logger;
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformApplicationBackgroundJobExecutor(
        IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory)
    {
        UnitOfWorkManager = unitOfWorkManager;
        Logger = loggerFactory.CreateLogger(GetType());
    }

    public virtual bool AutoOpenUow => true;

    public override void Execute()
    {
        try
        {
            if (AutoOpenUow)
                using (var uow = UnitOfWorkManager.Begin())
                {
                    ProcessAsync().WaitResult();

                    uow.CompleteAsync().WaitResult();
                }
            else
                ProcessAsync().WaitResult();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "[BackgroundJob] Job {BackgroundJobType_Name} execution was failed.", GetType().Name);
            throw;
        }
    }

    public abstract Task ProcessAsync();
}

public abstract class PlatformApplicationBackgroundJobExecutor<TParam> : PlatformBackgroundJobExecutor<TParam>
    where TParam : class
{
    protected readonly ILogger Logger;
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformApplicationBackgroundJobExecutor(
        IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory)
    {
        UnitOfWorkManager = unitOfWorkManager;
        Logger = loggerFactory.CreateLogger(GetType());
    }

    public override void Execute(TParam param)
    {
        try
        {
            using (var uow = UnitOfWorkManager.Begin())
            {
                ProcessAsync(param).WaitResult();
                uow.CompleteAsync().WaitResult();
            }
        }
        catch (Exception e)
        {
            Logger.LogError(
                e,
                "[BackgroundJob] Job {BackgroundJobType_Name} execution with param {BackgroundJob_Param} was failed.",
                GetType().Name,
                param?.AsJson());
            throw;
        }
    }

    public abstract Task ProcessAsync(TParam param);
}
