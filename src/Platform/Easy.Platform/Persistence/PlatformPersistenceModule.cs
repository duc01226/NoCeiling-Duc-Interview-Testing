using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
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
    bool ForCrossDbMigrationOnly { get; }

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
    public new const int DefaultExecuteInitPriority = PlatformModule.DefaultExecuteInitPriority + (ExecuteInitPriorityNextLevelDistance * 2);

    protected PlatformPersistenceModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    public static int DefaultDbInitAndMigrationRetryCount => PlatformEnvironment.IsDevelopment ? 5 : 10;

    public virtual bool ForCrossDbMigrationOnly => false;

    public virtual bool DisableDbInitializingAndMigration => false;

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    public abstract Task MigrateApplicationDataAsync(IServiceScope serviceScope);

    public abstract Task InitializeDb(IServiceScope serviceScope);

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformDbContext>(Assembly, ServiceLifeTime.Scoped);

        if (!ForCrossDbMigrationOnly)
        {
            RegisterUnitOfWorkManager(serviceCollection);
            serviceCollection.RegisterAllFromType<IUnitOfWork>(Assembly);
            RegisterRepositories(serviceCollection);

            RegisterInboxEventBusMessageRepository(serviceCollection);
            RegisterOutboxEventBusMessageRepository(serviceCollection);

            serviceCollection.RegisterAllFromType<IPersistenceService>(Assembly);
            serviceCollection.RegisterAllFromType<IPlatformDataMigrationExecutor>(Assembly);
        }
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
        return true;
    }

    /// <summary>
    /// EnableOutboxBusMessage feature by register the IPlatformOutboxBusMessageRepository
    /// </summary>
    protected virtual bool EnableOutboxBusMessage()
    {
        return true;
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
        if (ForCrossDbMigrationOnly) return;

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
    protected PlatformPersistenceModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    // Use this to lock to allow only ony background data migration run at a time
    public SemaphoreSlim BackgroundThreadDataMigrationLock { get; } = new(1, 1);

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    public override async Task MigrateApplicationDataAsync(IServiceScope serviceScope)
    {
        if (ForCrossDbMigrationOnly || DisableDbInitializingAndMigration) return;

        // if the db server container is not created on run docker compose,
        // the migration action could fail for network related exception. So that we do retry to ensure that Initialize action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                await serviceScope.ServiceProvider.GetRequiredService<TDbContext>().MigrateApplicationDataAsync(serviceScope.ServiceProvider);
            },
            sleepDurationProvider: retryAttempt => 10.Seconds(),
            retryCount: DefaultDbInitAndMigrationRetryCount,
            onBeforeThrowFinalExceptionFn: exception => Logger.LogError(
                exception,
                "[{DbContext}] {ExceptionType} detected on attempt MigrateApplicationDataAsync",
                typeof(TDbContext).Name,
                exception.GetType().Name));
    }

    public override async Task InitializeDb(IServiceScope serviceScope)
    {
        if (ForCrossDbMigrationOnly || DisableDbInitializingAndMigration) return;

        // if the db server container is not created on run docker compose,
        // the migration action could fail for network related exception. So that we do retry to ensure that Initialize action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
            async () =>
            {
                await serviceScope.ServiceProvider.GetRequiredService<TDbContext>().Initialize(serviceScope.ServiceProvider);
            },
            sleepDurationProvider: retryAttempt => 10.Seconds(),
            retryCount: DefaultDbInitAndMigrationRetryCount,
            onBeforeThrowFinalExceptionFn: exception => Logger.LogError(
                exception,
                "[{DbContext}] {ExceptionType} detected on attempt InitializeDb",
                typeof(TDbContext).Name,
                exception.GetType().Name));
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllForImplementation<TDbContext>(ServiceLifeTime.Scoped);
        RegisterPersistenceConfiguration(serviceCollection);

        base.InternalRegister(serviceCollection);
    }

    protected void RegisterPersistenceConfiguration(IServiceCollection serviceCollection)
    {
        serviceCollection.Register(
            sp => new PlatformPersistenceConfiguration<TDbContext>()
                .With(_ => _.ForCrossDbMigrationOnly = ForCrossDbMigrationOnly)
                .Pipe(_ => ConfigurePersistenceConfiguration(_, Configuration)));
    }

    protected virtual PlatformPersistenceConfiguration<TDbContext> ConfigurePersistenceConfiguration(
        PlatformPersistenceConfiguration<TDbContext> config,
        IConfiguration configuration)
    {
        return config;
    }
}
