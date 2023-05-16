using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Infrastructures.BackgroundJob;

public abstract class PlatformBackgroundJobModule : PlatformInfrastructureModule
{
    // PlatformBackgroundJobModule init after PersistenceModule but before other modules
    public new const int DefaultExecuteInitPriority = DefaultDependentOnPersistenceInitExecuteInitPriority;

    public PlatformBackgroundJobModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    public static int DefaultStartBackgroundJobProcessingRetryCount => PlatformEnvironment.IsDevelopment ? 5 : 10;

    /// <summary>
    /// Default return false.
    /// </summary>
    protected virtual bool AutoStartBackgroundJobProcessingOnInit => false;

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobExecutor>(Assembly);

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobScheduler>(
            Assembly,
            replaceStrategy: DependencyInjectionExtension.ReplaceServiceStrategy.ByService);

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobProcessingService>(
            Assembly,
            ServiceLifeTime.Singleton,
            replaceStrategy: DependencyInjectionExtension.ReplaceServiceStrategy.ByService);

        serviceCollection.RegisterHostedService<PlatformBackgroundJobStartProcessHostedService>();
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await base.InternalInit(serviceScope);

        await ReplaceAllLatestRecurringBackgroundJobs(serviceScope);

        if (AutoStartBackgroundJobProcessingOnInit)
            await StartBackgroundJobProcessing(serviceScope);
    }

    protected async Task StartBackgroundJobProcessing(IServiceScope serviceScope)
    {
        var backgroundJobProcessingService =
            serviceScope.ServiceProvider.GetRequiredService<IPlatformBackgroundJobProcessingService>();

        if (!backgroundJobProcessingService.Started())
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    await backgroundJobProcessingService.Start();
                },
                sleepDurationProvider: retryAttempt => 10.Seconds(),
                retryCount: DefaultStartBackgroundJobProcessingRetryCount,
                onRetry: (exception, timeSpan, currentRetry, ctx) =>
                {
                    var logger = serviceScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PlatformBackgroundJobModule));

                    logger.LogWarning(
                        exception,
                        "[StartBackgroundJobProcessing] Exception {ExceptionType} with message {Message} detected on attempt StartBackgroundJobProcessing {Retry} of {Retries}",
                        exception.GetType().Name,
                        exception.Message,
                        currentRetry,
                        DefaultStartBackgroundJobProcessingRetryCount);
                });
    }

    protected async Task ReplaceAllLatestRecurringBackgroundJobs(IServiceScope serviceScope)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var scheduler = serviceScope.ServiceProvider.GetRequiredService<IPlatformBackgroundJobScheduler>();

                var allCurrentRecurringJobExecutors = serviceScope.ServiceProvider
                    .GetServices<IPlatformBackgroundJobExecutor>()
                    .Where(p => PlatformRecurringJobAttribute.GetCronExpressionInfo(p.GetType()).IsNotNullOrEmpty())
                    .ToList();

                scheduler.ReplaceAllRecurringBackgroundJobs(allCurrentRecurringJobExecutors);
            },
            sleepDurationProvider: retryAttempt => 10.Seconds(),
            retryCount: DefaultStartBackgroundJobProcessingRetryCount,
            onRetry: (exception, timeSpan, currentRetry, ctx) =>
            {
                var logger = serviceScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PlatformBackgroundJobModule));

                logger.LogWarning(
                    exception,
                    "[Init][ReplaceAllLatestRecurringBackgroundJobs] Exception {ExceptionType} with message {Message} detected on attempt ReplaceAllLatestRecurringBackgroundJobs {Retry} of {Retries}",
                    exception.GetType().Name,
                    exception.Message,
                    currentRetry,
                    DefaultStartBackgroundJobProcessingRetryCount);
            });
    }
}
