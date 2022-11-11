using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Infrastructures;
using Easy.Platform.Persistence.DataMigration;
using Easy.Platform.Persistence.Domain;
using Easy.Platform.Persistence.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Persistence;

public interface IPlatformPersistenceModule : IPlatformModule
{
    /// <summary>
    /// Default false. Override this to true for db context used to migrate cross db data,
    /// do not need to run migration and register repositories
    /// </summary>
    bool ForReadDataOnly { get; }

    /// <summary>
    /// Default false. Override this to true for db context module db from
    /// other sub service but use the same shared module data in one micro-service group point to same db
    /// </summary>
    bool DisableDbInitializingAndMigration { get; }

    Task MigrateApplicationDataAsync(IServiceScope serviceScope);

    Task InitializeDb(IServiceScope serviceScope);

    public static async Task ExecuteDependencyPersistenceModuleMigrateApplicationData(
        List<Type> moduleTypeDependencies,
        IServiceProvider serviceProvider)
    {
        await moduleTypeDependencies
            .Where(moduleType => moduleType.IsAssignableTo(typeof(IPlatformPersistenceModule)))
            .Select(moduleType => new { ModuleType = moduleType, serviceProvider.GetService(moduleType).As<IPlatformPersistenceModule>().ExecuteInitPriority })
            .OrderByDescending(p => p.ExecuteInitPriority)
            .Select(p => p.ModuleType)
            .ForEachAsync(
                async moduleType =>
                {
                    await serviceProvider.ExecuteScopedAsync(
                        scope => scope.ServiceProvider.GetService(moduleType).As<IPlatformPersistenceModule>().MigrateApplicationDataAsync(scope));
                });
    }
}

/// <summary>
/// IPlatformDbContext.Initialize() is run on module init to init db context
/// </summary>
public abstract class PlatformPersistenceModule : PlatformModule, IPlatformPersistenceModule
{
    public new const int DefaultExecuteInitPriority = PlatformInfrastructureModule.DefaultExecuteInitPriority + 1;

    protected PlatformPersistenceModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    public static int DefaultDbInitAndMigrationRetryCount => PlatformEnvironment.IsDevelopment ? 5 : 10;

    public virtual bool ForReadDataOnly => false;

    public virtual bool DisableDbInitializingAndMigration => false;

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    public abstract Task MigrateApplicationDataAsync(IServiceScope serviceScope);

    public abstract Task InitializeDb(IServiceScope serviceScope);

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformDbContext>(Assembly, ServiceLifeTime.Scoped);

        RegisterUnitOfWorkManager(serviceCollection);
        serviceCollection.RegisterAllFromType<IUnitOfWork>(Assembly);
        RegisterRepositories(serviceCollection);

        RegisterInboxEventBusMessageRepository(serviceCollection);
        serviceCollection.Register(
            serviceType: typeof(PlatformInboxConfig),
            InboxConfigProvider,
            ServiceLifeTime.Transient,
            replaceIfExist: true,
            DependencyInjectionExtension.ReplaceServiceStrategy.ByService);

        RegisterOutboxEventBusMessageRepository(serviceCollection);
        serviceCollection.Register(
            serviceType: typeof(PlatformOutboxConfig),
            OutboxConfigProvider,
            ServiceLifeTime.Transient,
            replaceIfExist: true,
            DependencyInjectionExtension.ReplaceServiceStrategy.ByService);

        serviceCollection.RegisterAllFromType<IPersistenceService>(Assembly);
        serviceCollection.RegisterAllFromType<IPlatformDataMigrationExecutor>(Assembly);
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await base.InternalInit(serviceScope);

        await InitializeDb(serviceScope);
    }

    /// <summary>
    /// Override this function to limit the list of supported limited repository implementation for this persistence module
    /// </summary>
    protected virtual List<Type> RegisterLimitedRepositoryImplementationTypes()
    {
        return null;
    }

    protected virtual void RegisterInboxEventBusMessageRepository(IServiceCollection serviceCollection)
    {
        if (EnableInboxBusMessage())
            serviceCollection.RegisterAllFromType<IPlatformInboxBusMessageRepository>(Assembly);
    }

    protected virtual void RegisterOutboxEventBusMessageRepository(IServiceCollection serviceCollection)
    {
        if (EnableOutboxBusMessage())
            serviceCollection.RegisterAllFromType<IPlatformOutboxBusMessageRepository>(Assembly);
    }

    /// <summary>
    /// EnableInboxBusMessage feature by register the IPlatformInboxBusMessageRepository
    /// </summary>
    protected virtual bool EnableInboxBusMessage()
    {
        return false;
    }

    /// <summary>
    /// Support to custom the inbox config. Default return null
    /// </summary>
    protected virtual PlatformInboxConfig InboxConfigProvider(IServiceProvider serviceProvider)
    {
        return new PlatformInboxConfig();
    }

    /// <summary>
    /// EnableOutboxBusMessage feature by register the IPlatformOutboxBusMessageRepository
    /// </summary>
    protected virtual bool EnableOutboxBusMessage()
    {
        return false;
    }

    /// <summary>
    /// Support to custom the outbox config. Default return null
    /// </summary>
    protected virtual PlatformOutboxConfig OutboxConfigProvider(IServiceProvider serviceProvider)
    {
        return new PlatformOutboxConfig();
    }

    protected virtual void RegisterUnitOfWorkManager(IServiceCollection serviceCollection)
    {
        serviceCollection.Register<IUnitOfWorkManager, PlatformDefaultPersistenceUnitOfWorkManager>(ServiceLifeTime.Scoped);

        serviceCollection.RegisterAllFromType(
            typeof(IUnitOfWorkManager),
            Assembly,
            ServiceLifeTime.Scoped,
            replaceIfExist: true,
            replaceStrategy: DependencyInjectionExtension.ReplaceServiceStrategy.ByService);
    }

    private void RegisterRepositories(IServiceCollection serviceCollection)
    {
        if (ForReadDataOnly) return;

        if (RegisterLimitedRepositoryImplementationTypes()?.Any() == true)
            RegisterLimitedRepositoryImplementationTypes()
                .ForEach(repositoryImplementationType => serviceCollection.RegisterAllForImplementation(repositoryImplementationType));
        else
            serviceCollection.RegisterAllFromType<IPlatformRepository>(Assembly);
    }
}

/// <inheritdoc cref="PlatformPersistenceModule" />
public abstract class PlatformPersistenceModule<TDbContext> : PlatformPersistenceModule, IPlatformPersistenceModule
    where TDbContext : class, IPlatformDbContext
{
    public new const int DefaultExecuteInitPriority = PlatformInfrastructureModule.DefaultExecuteInitPriority + 1;

    protected PlatformPersistenceModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    public override async Task MigrateApplicationDataAsync(IServiceScope serviceScope)
    {
        if (ForReadDataOnly || DisableDbInitializingAndMigration) return;

        // if the db server container is not created on run docker compose,
        // the migration action could fail for network related exception. So that we do retry to ensure that Initialize action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                await serviceScope.ServiceProvider.GetRequiredService<TDbContext>().MigrateApplicationDataAsync(serviceScope.ServiceProvider);
            },
            sleepDurationProvider: retryAttempt => 10.Seconds(),
            retryCount: DefaultDbInitAndMigrationRetryCount,
            onRetry: (exception, timeSpan, currentRetry, ctx) =>
            {
                Logger.LogWarning(
                    exception,
                    "[{DbContext}] Exception {ExceptionType} with message {Message} detected on attempt MigrateApplicationDataAsync {retry} of {retries}",
                    typeof(TDbContext).Name,
                    exception.GetType().Name,
                    exception.Message,
                    currentRetry,
                    DefaultDbInitAndMigrationRetryCount);
            });
    }

    public override async Task InitializeDb(IServiceScope serviceScope)
    {
        if (ForReadDataOnly || DisableDbInitializingAndMigration) return;

        // if the db server container is not created on run docker compose,
        // the migration action could fail for network related exception. So that we do retry to ensure that Initialize action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                await serviceScope.ServiceProvider.GetRequiredService<TDbContext>().Initialize(serviceScope.ServiceProvider);
            },
            sleepDurationProvider: retryAttempt => 10.Seconds(),
            retryCount: DefaultDbInitAndMigrationRetryCount,
            onRetry: (exception, timeSpan, currentRetry, ctx) =>
            {
                Logger.LogWarning(
                    exception,
                    "[{DbContext}] Exception {ExceptionType} with message {Message} detected on attempt Initialize {retry} of {retries}",
                    typeof(TDbContext).Name,
                    exception.GetType().Name,
                    exception.Message,
                    currentRetry,
                    DefaultDbInitAndMigrationRetryCount);
            });
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllForImplementation<TDbContext>(ServiceLifeTime.Scoped);

        base.InternalRegister(serviceCollection);
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await base.InternalInit(serviceScope);

        await InitializeDb(serviceScope);
    }
}
