using System.Diagnostics;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.BackgroundJob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.BackgroundJob;

public interface IPlatformApplicationBackgroundJobExecutor
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformApplicationBackgroundJobExecutor)}");
}

public abstract class PlatformApplicationBackgroundJobExecutor<TParam> : PlatformBackgroundJobExecutor<TParam>, IPlatformApplicationBackgroundJobExecutor
    where TParam : class
{
    protected readonly IPlatformApplicationSettingContext ApplicationSettingContext;
    protected readonly IPlatformUnitOfWorkManager UnitOfWorkManager;

    public PlatformApplicationBackgroundJobExecutor(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, rootServiceProvider)
    {
        UnitOfWorkManager = unitOfWorkManager;
        ApplicationSettingContext = rootServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();
    }

    public virtual bool AutoOpenUow => true;

    public override bool LogDebugInformation()
    {
        return ApplicationSettingContext.IsDebugInformationMode;
    }

    protected override async Task InternalExecuteAsync(TParam param)
    {
        using (var activity = IPlatformApplicationBackgroundJobExecutor.ActivitySource.StartActivity($"BackgroundJob.{nameof(InternalExecuteAsync)}"))
        {
            activity?.SetTag("Type", GetType().FullName);
            activity?.SetTag("Param", param?.ToFormattedJson());

            if (ApplicationSettingContext.IsDebugInformationMode)
                Logger.LogInformation("{Type} {Method} STARTED", GetType().FullName, nameof(InternalExecuteAsync));

            if (AutoOpenUow)
            {
                await UnitOfWorkManager.ExecuteUowTask(() => ProcessAsync(param));
            }
            else
            {
                await ProcessAsync(param);

                await UnitOfWorkManager.TryCurrentActiveUowSaveChangesAsync();
            }

            if (ApplicationSettingContext.IsDebugInformationMode)
                Logger.LogInformation("{Type} {Method} STARTED", GetType().FullName, nameof(InternalExecuteAsync));
        }
    }
}

public abstract class PlatformApplicationBackgroundJobExecutor : PlatformApplicationBackgroundJobExecutor<object>
{
    protected PlatformApplicationBackgroundJobExecutor(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider) : base(unitOfWorkManager, loggerFactory, rootServiceProvider)
    {
    }
}
