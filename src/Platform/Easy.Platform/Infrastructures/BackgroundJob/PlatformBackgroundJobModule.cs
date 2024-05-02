using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Common.Validations.Extensions;
using Microsoft.AspNetCore.Builder;
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
    /// Override AutoUseDashboardUi = true to background job dashboard ui. Config via PlatformBackgroundJobUseDashboardUiOptions. Default Path is /BackgroundJobsDashboard
    /// </summary>
    public virtual bool AutoUseDashboardUi => false;

    public virtual PlatformBackgroundJobModule UseDashboardUi(IApplicationBuilder app, PlatformBackgroundJobUseDashboardUiOptions options = null)
    {
        return this;
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobExecutor>(GetServicesRegisterScanAssemblies());

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobScheduler>(
            GetServicesRegisterScanAssemblies(),
            replaceStrategy: DependencyInjectionExtension.CheckRegisteredStrategy.ByService,
            lifeTime: ServiceLifeTime.Singleton);

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobProcessingService>(
            GetServicesRegisterScanAssemblies(),
            ServiceLifeTime.Singleton,
            replaceStrategy: DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        serviceCollection.RegisterHostedService<PlatformBackgroundJobStartProcessHostedService>();
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await base.InternalInit(serviceScope);

        await ReplaceAllLatestRecurringBackgroundJobs(serviceScope);

        await StartBackgroundJobProcessing(serviceScope);

        if (AutoUseDashboardUi) UseDashboardUi(CurrentAppBuilder);
    }

    public async Task StartBackgroundJobProcessing(IServiceScope serviceScope)
    {
        var backgroundJobProcessingService =
            serviceScope.ServiceProvider.GetRequiredService<IPlatformBackgroundJobProcessingService>();

        if (!backgroundJobProcessingService.Started())
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                async () =>
                {
                    await backgroundJobProcessingService.Start();

                    Util.TaskRunner.QueueActionInBackground(ExecuteOnStartUpRecurringBackgroundJobImmediately, () => Logger);
                },
                sleepDurationProvider: retryAttempt => 10.Seconds(),
                retryCount: DefaultStartBackgroundJobProcessingRetryCount,
                onRetry: (exception, timeSpan, currentRetry, ctx) =>
                {
                    LoggerFactory.CreateLogger(typeof(PlatformBackgroundJobModule))
                        .LogError(
                            exception,
                            "[StartBackgroundJobProcessing] Exception {ExceptionType} detected on attempt StartBackgroundJobProcessing {Retry} of {Retries}",
                            exception.GetType().Name,
                            currentRetry,
                            DefaultStartBackgroundJobProcessingRetryCount);
                });
    }

    public async Task ExecuteOnStartUpRecurringBackgroundJobImmediately()
    {
        await IPlatformModule.WaitAllModulesInitiatedAsync(ServiceProvider, typeof(IPlatformModule), Logger, "execute on start-up recurring background job");

        await ServiceProvider.ExecuteInjectScopedAsync(
            (IPlatformBackgroundJobScheduler backgroundJobScheduler, IServiceProvider serviceProvider) =>
            {
                var allExecuteOnStartUpCurrentRecurringJobExecutors = serviceProvider
                    .GetServices<IPlatformBackgroundJobExecutor>()
                    .Where(p => PlatformRecurringJobAttribute.GetRecurringJobAttributeInfo(p.GetType()) is { ExecuteOnStartUp: true })
                    .ToList();

                allExecuteOnStartUpCurrentRecurringJobExecutors.ForEach(p => backgroundJobScheduler.Schedule<object>(p.GetType(), null, DateTimeOffset.UtcNow));
            });
    }

    public async Task ReplaceAllLatestRecurringBackgroundJobs(IServiceScope serviceScope)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var scheduler = serviceScope.ServiceProvider.GetRequiredService<IPlatformBackgroundJobScheduler>();

                var allCurrentRecurringJobExecutors = serviceScope.ServiceProvider
                    .GetServices<IPlatformBackgroundJobExecutor>()
                    .Where(p => PlatformRecurringJobAttribute.GetRecurringJobAttributeInfo(p.GetType()) != null)
                    .ToList();

                scheduler.ReplaceAllRecurringBackgroundJobs(allCurrentRecurringJobExecutors);
            },
            sleepDurationProvider: retryAttempt => 10.Seconds(),
            retryCount: DefaultStartBackgroundJobProcessingRetryCount,
            onRetry: (exception, timeSpan, currentRetry, ctx) =>
            {
                LoggerFactory.CreateLogger(typeof(PlatformBackgroundJobModule))
                    .LogError(
                        exception,
                        "[Init][ReplaceAllLatestRecurringBackgroundJobs] Exception {ExceptionType} detected on attempt ReplaceAllLatestRecurringBackgroundJobs {Retry} of {Retries}",
                        exception.GetType().Name,
                        currentRetry,
                        DefaultStartBackgroundJobProcessingRetryCount);
            });
    }
}

/// <summary>
/// Config BackgroundJobsDashboard. Default path is: /BackgroundJobsDashboard
/// </summary>
public class PlatformBackgroundJobUseDashboardUiOptions
{
    /// <summary>
    /// Default is "/BackgroundJobsDashboard"
    /// </summary>
    public string DashboardUiPathStart { get; set; } = "/BackgroundJobsDashboard";

    public bool UseAuthentication { get; set; }

    public BasicAuthentications BasicAuthentication { get; set; }

    public void EnsureValid()
    {
        this.Validate(
                p => p.BasicAuthentication == null || (p.BasicAuthentication.UserName.IsNotNullOrEmpty() && p.BasicAuthentication.Password.IsNotNullOrEmpty()),
                "PlatformBackgroundJobUseDashboardUiOptions BasicAuthentication UserName and Password must be not null or empty")
            .And(p => p.UseAuthentication == false || p.BasicAuthentication != null, "UseAuthentication is True must come with one of BasicAuthentication")
            .EnsureValid();
    }

    public class BasicAuthentications
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
