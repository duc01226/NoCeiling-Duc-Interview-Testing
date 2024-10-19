using System.Reflection;
using Easy.Platform.Application.BackgroundJob;
using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Application.Cqrs.Queries;
using Easy.Platform.Application.Domain;
using Easy.Platform.Application.HostingBackgroundServices;
using Easy.Platform.Application.MessageBus;
using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.MessageBus.Producers;
using Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Application.RequestContext;
using Easy.Platform.Application.Services;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures.Abstract;
using Easy.Platform.Infrastructures.BackgroundJob;
using Easy.Platform.Infrastructures.Caching;
using Easy.Platform.Infrastructures.MessageBus;
using Easy.Platform.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application;

public interface IPlatformApplicationModule : IPlatformModule
{
    Task SeedData(IServiceScope serviceScope);

    Task ClearDistributedCache(
        PlatformApplicationAutoClearDistributedCacheOnInitOptions options,
        IServiceScope serviceScope);
}

public abstract class PlatformApplicationModule : PlatformModule, IPlatformApplicationModule
{
    protected PlatformApplicationModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    protected override bool AutoScanAssemblyRegisterCqrs => true;

    /// <summary>
    /// Override this to true to auto register default caching module, which include default memory caching repository.
    /// <br></br>
    /// Don't need to auto register if you have register a caching module manually
    /// </summary>
    protected virtual bool AutoRegisterDefaultCaching => true;

    /// <summary>
    /// Default is True. Override this return to False if you need to seed data manually
    /// </summary>
    protected virtual bool AutoSeedApplicationDataOnInit => true;

    /// <summary>
    /// Set min thread pool then default to increase and fix some performance issues. Article:
    /// https://medium.com/@jaiadityarathore/dotnet-core-threadpool-bef2f5a37888
    /// https://github.com/StackExchange/StackExchange.Redis/issues/2332
    /// Default equal DefaultParallelIoTaskMaxConcurrent + extra available one DefaultNumberOfParallelIoTasksPerCpuRatio
    /// </summary>
    protected virtual int MinWorkerThreadPool => Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent + Util.TaskRunner.DefaultNumberOfParallelIoTasksPerCpuRatio;

    protected virtual int MinIoThreadPool => Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent + Util.TaskRunner.DefaultNumberOfParallelIoTasksPerCpuRatio;

    public virtual bool AutoClearMemoryEnabled => true;
    public virtual int AutoClearMemoryIntervalTimeSeconds => PlatformAutoClearMemoryHostingBackgroundService.DefaultProcessTriggerIntervalTimeSeconds;

    /// <summary>
    /// Seeds the application data into the database.
    /// </summary>
    /// <param name="serviceScope">The service scope which provides services for seeding data.</param>
    /// <remarks>
    /// This method will attempt to seed the data multiple times if the initial seeding fails,
    /// due to the possibility of the database server not being fully initialized.
    /// </remarks>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    public async Task SeedData(IServiceScope serviceScope)
    {
        //if the db server is not initiated, SeedData could fail.
        //So that we do retry to ensure that SeedData action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                await ExecuteDataSeeders(
                    serviceScope.ServiceProvider
                        .GetServices<IPlatformApplicationDataSeeder>()
                        .DistinctBy(p => p.GetType())
                        .Where(p => p.DelaySeedingInBackgroundBySeconds == 0)
                        .GroupBy(p => p.SeedOrder)
                        .OrderBy(p => p.Key)
                        .ToList());

                await ExecuteDataSeeders(
                    serviceScope.ServiceProvider
                        .GetServices<IPlatformApplicationDataSeeder>()
                        .DistinctBy(p => p.GetType())
                        .Where(p => p.DelaySeedingInBackgroundBySeconds > 0)
                        .GroupBy(p => p.SeedOrder)
                        .OrderBy(p => p.Key)
                        .ToList(),
                    inBackground: true);
            },
            retryAttempt => 10.Seconds(),
            10,
            onRetry: (exception, timeSpan, retry, ctx) =>
            {
                Logger.LogError(
                    exception.BeautifyStackTrace(),
                    "Exception {ExceptionType} detected on attempt SeedData {Retry}",
                    exception.GetType().Name,
                    retry);
            });

        static async Task ExecuteDataSeederWithLog(IPlatformApplicationDataSeeder dataSeeder, ILogger logger)
        {
            logger.LogInformation("[SeedData] {DataSeeder} STARTED.", dataSeeder.GetType().Name);

            await dataSeeder.SeedData();

            logger.LogInformation("[SeedData] {DataSeeder} FINISHED.", dataSeeder.GetType().Name);
        }

        async Task ExecuteDataSeeders(List<IGrouping<int, IPlatformApplicationDataSeeder>> dataSeeders, bool inBackground = false)
        {
            if (inBackground)
                Util.TaskRunner.QueueActionInBackground(
                    async () => await RunDataSeeders(dataSeeders, inNewScope: true),
                    loggerFactory: () => CreateLogger(LoggerFactory));
            else
                await RunDataSeeders(dataSeeders);
        }

        async Task RunDataSeeders(List<IGrouping<int, IPlatformApplicationDataSeeder>> dataSeederGroups, bool inNewScope = false)
        {
            await dataSeederGroups.ForEachAsync(
                async dataSeederGroup =>
                {
                    await dataSeederGroup.ParallelAsync(
                        async seeder =>
                        {
                            if (seeder.DelaySeedingInBackgroundBySeconds > 0)
                            {
                                Logger.LogInformation(
                                    "[SeedData] {Seeder} is SCHEDULED running in background after {DelaySeedingInBackgroundBySeconds} seconds.",
                                    seeder.GetType().Name,
                                    seeder.DelaySeedingInBackgroundBySeconds);

                                await Task.Delay(seeder.DelaySeedingInBackgroundBySeconds.Seconds());
                            }

                            if (inNewScope)
                                using (var newScope = ServiceProvider.CreateScope())
                                {
                                    var dataSeeder = newScope.ServiceProvider.GetService(seeder.GetType()).As<IPlatformApplicationDataSeeder>();

                                    await ExecuteDataSeederWithLog(dataSeeder, Logger);
                                }
                            else
                                await ExecuteDataSeederWithLog(seeder, Logger);
                        });
                });
        }
    }

    /// <summary>
    /// Clears the distributed cache based on the provided options and service scope.
    /// </summary>
    /// <param name="options">The options for auto clearing the distributed cache on initialization.</param>
    /// <param name="serviceScope">The service scope used to get the cache provider.</param>
    /// <remarks>
    /// If the cache server is not initiated, this method could fail. Therefore, it uses a retry mechanism to ensure successful execution.
    /// </remarks>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task ClearDistributedCache(
        PlatformApplicationAutoClearDistributedCacheOnInitOptions options,
        IServiceScope serviceScope)
    {
        //if the cache server is not initiated, ClearDistributedCache could fail.
        //So that we do retry to ensure that ClearDistributedCache action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var cacheProvider = serviceScope.ServiceProvider.GetService<IPlatformCacheRepositoryProvider>();

                var distributedCacheRepository = cacheProvider?.TryGet(PlatformCacheRepositoryType.Distributed);

                if (distributedCacheRepository != null)
                    await distributedCacheRepository.RemoveAsync(p => options.AutoClearContexts.Contains(p.Context));
            },
            retryAttempt => 10.Seconds(),
            10,
            onRetry: (exception, timeSpan, retry, ctx) =>
            {
                Logger.LogError(
                    exception.BeautifyStackTrace(),
                    "Exception {ExceptionType} detected on attempt ClearDistributedCache {Retry}",
                    exception.GetType().Name,
                    retry);
            });
    }

    public override string[] TracingSources()
    {
        return
        [
            IPlatformCqrsCommandApplicationHandler.ActivitySource.Name,
            IPlatformCqrsQueryApplicationHandler.ActivitySource.Name,
            IPlatformApplicationBackgroundJobExecutor.ActivitySource.Name,
            IPlatformDbContext.ActivitySource.Name
        ];
    }

    /// <summary>
    /// <inheritdoc cref="PlatformModule.GetServicesRegisterScanAssemblies" />  <br></br>
    /// For ApplicationModule, by default do not support scan parent application module
    /// </summary>
    public override List<Assembly> GetServicesRegisterScanAssemblies()
    {
        return [Assembly];
    }

    /// <summary>
    /// Support to custom the inbox config. Default return null
    /// </summary>
    protected virtual PlatformInboxConfig InboxConfigProvider(IServiceProvider serviceProvider)
    {
        return new PlatformInboxConfig();
    }

    /// <summary>
    /// Support to custom the outbox config. Default return null
    /// </summary>
    protected virtual PlatformOutboxConfig OutboxConfigProvider(IServiceProvider serviceProvider)
    {
        return new PlatformOutboxConfig();
    }

    protected override void RegisterHelpers(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformHelper>(GetServicesRegisterScanAssemblies());
        serviceCollection.RegisterAllFromType<IPlatformHelper>(GetServicesRegisterScanAssemblies());
    }

    /// <summary>
    /// Executes the seed data method of all dependent application modules in the order of their initialization priority.
    /// </summary>
    /// <param name="moduleTypeDependencies">A list of types representing the dependencies of the application module.</param>
    /// <param name="serviceProvider">An instance of IServiceProvider to resolve dependencies.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public static async Task ExecuteDependencyApplicationModuleSeedData(List<Type> moduleTypeDependencies, IServiceProvider serviceProvider)
    {
        await moduleTypeDependencies
            .Where(moduleType => moduleType.IsAssignableTo(typeof(IPlatformApplicationModule)))
            .Select(moduleType => new { ModuleType = moduleType, serviceProvider.GetService(moduleType).As<IPlatformApplicationModule>().ExecuteInitPriority })
            .OrderByDescending(p => p.ExecuteInitPriority)
            .Select(p => p.ModuleType)
            .ForEachAsync(
                async moduleType =>
                {
                    await serviceProvider.ExecuteScopedAsync(
                        scope => scope.ServiceProvider.GetService(moduleType).As<IPlatformApplicationModule>().SeedData(scope));
                });
    }

    public async Task ExecuteDependencyApplicationModuleSeedData()
    {
        await ExecuteDependencyApplicationModuleSeedData(
            ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
            ServiceProvider);
    }

    /// <summary>
    /// Override this factory method to register default PlatformApplicationSettingContext if application do not
    /// have any implementation of IPlatformApplicationSettingContext in the Assembly to be registered.
    /// </summary>
    protected virtual PlatformApplicationSettingContext DefaultApplicationSettingContextFactory(
        IServiceProvider serviceProvider)
    {
        return new PlatformApplicationSettingContext
        {
            ApplicationName = Assembly.GetName().Name,
            ApplicationAssembly = Assembly
        };
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformApplicationDataSeeder>(GetServicesRegisterScanAssemblies(), ServiceLifeTime.Scoped);
        serviceCollection.RegisterAllSelfImplementationFromType<IPlatformCqrsEventApplicationHandler>(GetServicesRegisterScanAssemblies());
        RegisterMessageBus(serviceCollection);
        RegisterApplicationSettingContext(serviceCollection);
        RegisterDefaultApplicationRequestContext(serviceCollection);
        serviceCollection.RegisterIfServiceNotExist<IPlatformUnitOfWorkManager, PlatformPseudoApplicationUnitOfWorkManager>(ServiceLifeTime.Scoped);
        serviceCollection.RegisterAllFromType<IPlatformApplicationService>(GetServicesRegisterScanAssemblies());

        serviceCollection.RegisterAllFromType<IPlatformDbContext>(GetServicesRegisterScanAssemblies(), ServiceLifeTime.Scoped);
        serviceCollection.RegisterAllFromType<IPlatformInfrastructureService>(GetServicesRegisterScanAssemblies());
        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobExecutor>(GetServicesRegisterScanAssemblies());

        if (AutoRegisterDefaultCaching)
            RegisterRuntimeModuleDependencies<PlatformCachingModule>(serviceCollection);

        GetServicesRegisterScanAssemblies().ForEach(assembly => serviceCollection.RegisterHostedServicesFromType(assembly, typeof(PlatformHostingBackgroundService)));

        if (AutoClearMemoryEnabled)
            serviceCollection.RegisterHostedService(
                sp => new PlatformAutoClearMemoryHostingBackgroundService(sp, sp.GetRequiredService<ILoggerFactory>(), AutoClearMemoryIntervalTimeSeconds));

        serviceCollection.Register<IPlatformApplicationBackgroundJobScheduler, PlatformApplicationBackgroundJobScheduler>();
        serviceCollection.Register<IPlatformBackgroundJobSchedulerCarryRequestContextService, PlatformApplicationBackgroundJobSchedulerCarryRequestContextService>();
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        ThreadPool.SetMinThreads(MinWorkerThreadPool, MinIoThreadPool);

        await IPlatformPersistenceModule.ExecuteDependencyPersistenceModuleMigrateApplicationData(
            ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
            ServiceProvider);

        if (IsRootModule && AutoSeedApplicationDataOnInit)
            await ExecuteDependencyApplicationModuleSeedData();

        var autoClearDistributedCacheOnInitOptions = AutoClearDistributedCacheOnInitOptions(serviceScope);
        if (autoClearDistributedCacheOnInitOptions.EnableAutoClearDistributedCacheOnInit)
            await ClearDistributedCache(autoClearDistributedCacheOnInitOptions, serviceScope);

        if (AutoRegisterDefaultCaching)
            await serviceScope.ServiceProvider.GetRequiredService<PlatformCachingModule>()
                .With(p => p.IsChildModule = true)
                .Init(CurrentAppBuilder);
    }

    /// <summary>
    /// Gets the options for automatically clearing the distributed cache on initialization.
    /// </summary>
    /// <param name="serviceScope">The service scope.</param>
    /// <returns>A <see cref="PlatformApplicationAutoClearDistributedCacheOnInitOptions" /> object that represents the options for automatically clearing the distributed cache on initialization.</returns>
    protected virtual PlatformApplicationAutoClearDistributedCacheOnInitOptions
        AutoClearDistributedCacheOnInitOptions(IServiceScope serviceScope)
    {
        var applicationSettingContext =
            serviceScope.ServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();

        return new PlatformApplicationAutoClearDistributedCacheOnInitOptions
        {
            EnableAutoClearDistributedCacheOnInit = true,
            AutoClearContexts =
            [
                applicationSettingContext.ApplicationName
            ]
        };
    }

    private void RegisterApplicationSettingContext(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformApplicationSettingContext>(GetServicesRegisterScanAssemblies());

        // If there is no custom implemented class type of IPlatformApplicationSettingContext in application,
        // register default PlatformApplicationSettingContext from result of DefaultApplicationSettingContextFactory
        // WHY: To support custom IPlatformApplicationSettingContext if you want to or just use the default from DefaultApplicationSettingContextFactory
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformApplicationSettingContext)))
            serviceCollection.Register<IPlatformApplicationSettingContext>(DefaultApplicationSettingContextFactory, ServiceLifeTime.Singleton);
    }

    private static void RegisterDefaultApplicationRequestContext(IServiceCollection serviceCollection)
    {
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformApplicationRequestContextAccessor)))
            serviceCollection.Register(
                typeof(IPlatformApplicationRequestContextAccessor),
                typeof(PlatformDefaultApplicationRequestContextAccessor),
                ServiceLifeTime.Singleton,
                true,
                DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
    }

    /// <summary>
    /// Registers the message bus services in the provided service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add the services to.</param>
    private void RegisterMessageBus(IServiceCollection serviceCollection)
    {
        serviceCollection.Register<IPlatformMessageBusScanner, PlatformApplicationMessageBusScanner>(ServiceLifeTime.Singleton);

        serviceCollection.Register<IPlatformApplicationBusMessageProducer, PlatformApplicationBusMessageProducer>();
        serviceCollection.RegisterAllSelfImplementationFromType(
            typeof(IPlatformCqrsEventBusMessageProducer<>),
            GetServicesRegisterScanAssemblies());
        serviceCollection.RegisterAllSelfImplementationFromType(
            typeof(PlatformCqrsCommandEventBusMessageProducer<>),
            GetServicesRegisterScanAssemblies());
        serviceCollection.RegisterAllSelfImplementationFromType(
            typeof(PlatformCqrsEntityEventBusMessageProducer<,,>),
            GetServicesRegisterScanAssemblies());

        serviceCollection.RegisterAllSelfImplementationFromType(
            typeof(IPlatformMessageBusConsumer),
            typeof(PlatformApplicationModule).Assembly);
        serviceCollection.RegisterAllSelfImplementationFromType(
            typeof(IPlatformMessageBusConsumer),
            GetServicesRegisterScanAssemblies());
        serviceCollection.RegisterAllSelfImplementationFromType(
            typeof(IPlatformApplicationMessageBusConsumer<>),
            GetServicesRegisterScanAssemblies());

        serviceCollection.RegisterHostedService<PlatformInboxBusMessageCleanerHostedService>();
        serviceCollection.RegisterHostedService<PlatformConsumeInboxBusMessageHostedService>();
        serviceCollection.Register(
            typeof(PlatformInboxConfig),
            InboxConfigProvider,
            ServiceLifeTime.Singleton,
            true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        serviceCollection.RegisterHostedService<PlatformOutboxBusMessageCleanerHostedService>();
        serviceCollection.RegisterHostedService<PlatformSendOutboxBusMessageHostedService>();
        serviceCollection.Register(
            typeof(PlatformOutboxConfig),
            OutboxConfigProvider,
            ServiceLifeTime.Singleton,
            true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
    }
}

public class PlatformApplicationAutoClearDistributedCacheOnInitOptions
{
    private HashSet<string> autoClearContexts;
    public bool EnableAutoClearDistributedCacheOnInit { get; set; }

    public HashSet<string> AutoClearContexts
    {
        get => autoClearContexts;
        set => autoClearContexts = value?.Select(PlatformCacheKey.AutoFixKeyPartValue).ToHashSet();
    }
}
