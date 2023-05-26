using Easy.Platform.Application.BackgroundJob;
using Easy.Platform.Application.Context;
using Easy.Platform.Application.Context.UserContext;
using Easy.Platform.Application.Context.UserContext.Default;
using Easy.Platform.Application.Cqrs.Commands;
using Easy.Platform.Application.Cqrs.Events;
using Easy.Platform.Application.Cqrs.Queries;
using Easy.Platform.Application.Domain;
using Easy.Platform.Application.MessageBus;
using Easy.Platform.Application.MessageBus.Consumers;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.MessageBus.Producers;
using Easy.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
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

    public async Task SeedData(IServiceScope serviceScope)
    {
        //if the db server is not initiated, SeedData could fail.
        //So that we do retry to ensure that SeedData action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                var dataSeeders = serviceScope.ServiceProvider
                    .GetServices<IPlatformApplicationDataSeeder>()
                    .DistinctBy(p => p.GetType())
                    .OrderBy(p => p.SeedOrder)
                    .ThenBy(p => p.DelaySeedingInBackgroundBySeconds);

                await dataSeeders.ForEachAsync(
                    async seeder =>
                    {
                        if (seeder.DelaySeedingInBackgroundBySeconds > 0)
                        {
                            Logger.LogInformation(
                                $"[SeedData] {seeder.GetType().Name} is scheduled running in background after {seeder.DelaySeedingInBackgroundBySeconds} seconds.");

                            Util.TaskRunner.QueueActionInBackground(
                                action: () => ExecuteSeedingWithNewScopeInBackground(seeder.GetType(), Logger),
                                () => CreateLogger(PlatformGlobal.LoggerFactory),
                                delayTimeSeconds: seeder.DelaySeedingInBackgroundBySeconds);
                        }
                        else
                        {
                            await ExecuteDataSeederWithLog(seeder, Logger);
                        }
                    });
            },
            retryAttempt => 10.Seconds(),
            retryCount: 10,
            onRetry: (exception, timeSpan, retry, ctx) =>
            {
                if (retry >= MinimumRetryTimesToWarning)
                    Logger.LogWarning(
                        exception,
                        "Exception {ExceptionType} detected on attempt SeedData {Retry}",
                        exception.GetType().Name,
                        retry);
            });

        // Need to execute in background with service instance new scope
        // if not, the scope will be disposed, which lead to the seed data will be failed
        static async Task ExecuteSeedingWithNewScopeInBackground(Type seederType, ILogger logger)
        {
            try
            {
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                    async () =>
                    {
                        using (var newScope = PlatformGlobal.RootServiceProvider.CreateScope())
                        {
                            var dataSeeder = newScope.ServiceProvider
                                .GetServices<IPlatformApplicationDataSeeder>()
                                .First(_ => _.GetType() == seederType);

                            await ExecuteDataSeederWithLog(dataSeeder, logger);
                        }
                    },
                    retryAttempt => 15.Seconds(),
                    retryCount: 20,
                    onRetry: (ex, timeSpan, currentRetry, context) =>
                    {
                        if (currentRetry >= MinimumRetryTimesToWarning)
                            logger.LogWarning(
                                ex,
                                $"[SeedData] Retry seed data in background {seederType.Name}.");
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    $"[SeedData] Seed data in background {seederType.Name} failed.");
            }
        }

        static async Task ExecuteDataSeederWithLog(IPlatformApplicationDataSeeder dataSeeder, ILogger logger)
        {
            logger.LogInformation($"[SeedData] {dataSeeder.GetType().Name} started.");

            await dataSeeder.SeedData();

            logger.LogInformation($"[SeedData] {dataSeeder.GetType().Name} finished.");
        }
    }

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
                    await distributedCacheRepository.RemoveAsync(
                        p => options.AutoClearContexts.Contains(p.Context));
            },
            retryAttempt => 10.Seconds(),
            retryCount: 10,
            onRetry: (exception, timeSpan, retry, ctx) =>
            {
                if (retry >= MinimumRetryTimesToWarning)
                    Logger.LogWarning(
                        exception,
                        "Exception {ExceptionType} detected on attempt ClearDistributedCache {Retry}",
                        exception.GetType().Name,
                        retry);
            });
    }

    public override string[] TracingSources()
    {
        return Util.ListBuilder.NewArray(
            IPlatformCqrsCommandApplicationHandler.ActivitySource.Name,
            IPlatformCqrsQueryApplicationHandler.ActivitySource.Name,
            IPlatformApplicationBackgroundJobExecutor.ActivitySource.Name);
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
        serviceCollection.RegisterAllFromType<IPlatformHelper>(typeof(PlatformApplicationModule).Assembly);
        serviceCollection.RegisterAllFromType<IPlatformHelper>(Assembly);
    }

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
            moduleTypeDependencies: ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
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

        serviceCollection.RegisterAllFromType<IPlatformApplicationDataSeeder>(Assembly, ServiceLifeTime.Scoped);
        serviceCollection.RegisterAllFromType<IPlatformCqrsEventApplicationHandler>(Assembly);
        RegisterMessageBus(serviceCollection);
        RegisterApplicationSettingContext(serviceCollection);
        RegisterDefaultApplicationUserContext(serviceCollection);
        serviceCollection.RegisterIfServiceNotExist<IUnitOfWorkManager, PlatformPseudoApplicationUnitOfWorkManager>(ServiceLifeTime.Scoped);

        serviceCollection.RegisterAllFromType<IPlatformDbContext>(Assembly, ServiceLifeTime.Scoped);
        serviceCollection.RegisterAllFromType<IPlatformInfrastructureService>(Assembly);
        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobExecutor>(Assembly);

        if (AutoRegisterDefaultCaching)
            RegisterRuntimeModuleDependencies<PlatformCachingModule>(serviceCollection);

        serviceCollection.Register(
            serviceType: typeof(PlatformInboxConfig),
            InboxConfigProvider,
            ServiceLifeTime.Transient,
            replaceIfExist: true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
        serviceCollection.Register(
            serviceType: typeof(PlatformOutboxConfig),
            OutboxConfigProvider,
            ServiceLifeTime.Transient,
            replaceIfExist: true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await IPlatformPersistenceModule.ExecuteDependencyPersistenceModuleMigrateApplicationData(
            moduleTypeDependencies: ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
            ServiceProvider);

        if (IsRootModule && AutoSeedApplicationDataOnInit)
            await ExecuteDependencyApplicationModuleSeedData();

        var autoClearDistributedCacheOnInitOptions = AutoClearDistributedCacheOnInitOptions(serviceScope);
        if (autoClearDistributedCacheOnInitOptions.EnableAutoClearDistributedCacheOnInit)
            await ClearDistributedCache(autoClearDistributedCacheOnInitOptions, serviceScope);
    }

    protected virtual PlatformApplicationAutoClearDistributedCacheOnInitOptions
        AutoClearDistributedCacheOnInitOptions(IServiceScope serviceScope)
    {
        var applicationSettingContext =
            serviceScope.ServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();

        return new PlatformApplicationAutoClearDistributedCacheOnInitOptions
        {
            EnableAutoClearDistributedCacheOnInit = true,
            AutoClearContexts = new HashSet<string>
            {
                applicationSettingContext.ApplicationName
            }
        };
    }

    private void RegisterApplicationSettingContext(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformApplicationSettingContext>(Assembly);

        // If there is no custom implemented class type of IPlatformApplicationSettingContext in application,
        // register default PlatformApplicationSettingContext from result of DefaultApplicationSettingContextFactory
        // WHY: To support custom IPlatformApplicationSettingContext if you want to or just use the default from DefaultApplicationSettingContextFactory
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformApplicationSettingContext)))
            serviceCollection.Register<IPlatformApplicationSettingContext>(DefaultApplicationSettingContextFactory);
    }

    private void RegisterDefaultApplicationUserContext(IServiceCollection serviceCollection)
    {
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformApplicationUserContextAccessor)))
            serviceCollection.Register(
                typeof(IPlatformApplicationUserContextAccessor),
                typeof(PlatformDefaultApplicationUserContextAccessor),
                ServiceLifeTime.Singleton,
                replaceIfExist: true,
                DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
    }

    private void RegisterMessageBus(IServiceCollection serviceCollection)
    {
        serviceCollection.Register<IPlatformMessageBusScanner, PlatformApplicationMessageBusScanner>(ServiceLifeTime.Singleton);

        serviceCollection.Register<IPlatformApplicationBusMessageProducer, PlatformApplicationBusMessageProducer>();
        serviceCollection.RegisterAllFromType(
            typeof(IPlatformCqrsEventBusMessageProducer<>),
            Assembly);
        serviceCollection.RegisterAllFromType(
            typeof(PlatformCqrsCommandEventBusMessageProducer<>),
            Assembly);
        serviceCollection.RegisterAllFromType(
            typeof(PlatformCqrsEntityEventBusMessageProducer<,>),
            Assembly);

        serviceCollection.RegisterAllFromType(
            typeof(IPlatformMessageBusConsumer),
            typeof(PlatformApplicationModule).Assembly);
        serviceCollection.RegisterAllFromType(
            typeof(IPlatformMessageBusConsumer),
            Assembly);
        serviceCollection.RegisterAllFromType(
            typeof(IPlatformApplicationMessageBusConsumer<>),
            Assembly);

        if (serviceCollection.NotExist(PlatformInboxBusMessageCleanerHostedService.MatchImplementation))
            serviceCollection.RegisterHostedService<PlatformInboxBusMessageCleanerHostedService>();
        if (serviceCollection.NotExist(PlatformConsumeInboxBusMessageHostedService.MatchImplementation))
            serviceCollection.RegisterHostedService<PlatformConsumeInboxBusMessageHostedService>();
        serviceCollection.RegisterIfServiceNotExist<PlatformInboxConfig, PlatformInboxConfig>();

        if (serviceCollection.NotExist(PlatformOutboxBusMessageCleanerHostedService.MatchImplementation))
            serviceCollection.RegisterHostedService<PlatformOutboxBusMessageCleanerHostedService>();
        if (serviceCollection.NotExist(PlatformSendOutboxBusMessageHostedService.MatchImplementation))
            serviceCollection.RegisterHostedService<PlatformSendOutboxBusMessageHostedService>();
        serviceCollection.RegisterIfServiceNotExist<PlatformOutboxConfig, PlatformOutboxConfig>();
    }
}

public sealed class PlatformApplicationAutoClearDistributedCacheOnInitOptions
{
    private HashSet<string> autoClearContexts;
    public bool EnableAutoClearDistributedCacheOnInit { get; set; }

    public HashSet<string> AutoClearContexts
    {
        get => autoClearContexts;
        set => autoClearContexts = value?.Select(PlatformCacheKey.AutoFixKeyPartValue).ToHashSet();
    }
}
