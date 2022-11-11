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
    public PlatformBackgroundJobModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    public static int DefaultStartBackgroundJobProcessingRetryCount => PlatformEnvironment.IsDevelopment ? 5 : 10;

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

        await StartBackgroundJobProcessing(serviceScope);

        await ReplaceAllLatestRecurringBackgroundJobs(serviceScope);
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
                    var logger = serviceScope.ServiceProvider.GetService<ILoggerFactory>()!.CreateLogger(GetType());

                    logger.LogWarning(
                        exception,
                        "[StartBackgroundJobProcessing] Exception {ExceptionType} with message {Message} detected on attempt StartBackgroundJobProcessing {retry} of {retries}",
                        exception.GetType().Name,
                        exception.Message,
                        currentRetry,
                        DefaultStartBackgroundJobProcessingRetryCount);
                });
    }

    protected Task ReplaceAllLatestRecurringBackgroundJobs(IServiceScope serviceScope)
    {
        var scheduler = serviceScope.ServiceProvider.GetRequiredService<IPlatformBackgroundJobScheduler>();

        var allCurrentRecurringJobExecutors = serviceScope.ServiceProvider
            .GetServices<IPlatformBackgroundJobExecutor>()
            .Where(p => !string.IsNullOrEmpty(PlatformRecurringJobAttribute.GetCronExpressionInfo(p.GetType())))
            .ToList();

        scheduler.ReplaceAllRecurringBackgroundJobs(allCurrentRecurringJobExecutors);

        return Task.CompletedTask;
    }
}
