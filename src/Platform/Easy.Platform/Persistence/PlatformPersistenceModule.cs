using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
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
/// Represents an abstract base class for platform persistence modules.
/// </summary>
/// <remarks>
/// This class provides a set of methods and properties to manage the persistence layer of the platform.
/// It includes functionalities for database initialization, migration, and tracing sources.
/// It also provides methods for registering services and initializing the module.
/// </remarks>
public abstract class PlatformPersistenceModule : PlatformModule, IPlatformPersistenceModule
{
    public new const int DefaultExecuteInitPriority = PlatformModule.DefaultExecuteInitPriority + (ExecuteInitPriorityNextLevelDistance * 2);

    protected PlatformPersistenceModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    public static int DefaultDbInitAndMigrationRetryCount => PlatformEnvironment.IsDevelopment ? 5 : 10;

    public static int DefaultDbInitAndMigrationRetryDelaySeconds => PlatformEnvironment.IsDevelopment ? 15 : 30;

    public override string[] TracingSources()
    {
        return Util.ListBuilder.NewArray(
            IPlatformRepository.ActivitySource.Name,
            IPlatformUnitOfWork.ActivitySource.Name,
            IPlatformUnitOfWorkManager.ActivitySource.Name);
    }

    /// <summary>
    /// Gets a value indicating whether the current persistence module is used only for cross-database migrations.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is for cross-database migrations only; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// When this property is set to <c>true</c>, the persistence module will not register certain services and repositories, and will not perform database initialization and migration.
    /// </remarks>
    public virtual bool ForCrossDbMigrationOnly => false;

    /// <summary>
    /// Gets a value indicating whether the database initialization and migration is disabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the database initialization and migration is disabled; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// If this property is set to <c>true</c>, the database initialization and migration will not be performed.
    /// This can be useful in scenarios where the database schema is managed outside the application, or for testing purposes.
    /// </remarks>
    public virtual bool DisableDbInitializingAndMigration => false;

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    /// <summary>
    /// Asynchronously migrates the application data.
    /// </summary>
    /// <param name="serviceScope">The service scope.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method is responsible for migrating the application data. It is an abstract method, meaning it must be implemented in any non-abstract class that extends PlatformPersistenceModule.
    /// </remarks>
    public abstract Task MigrateApplicationDataAsync(IServiceScope serviceScope);

    /// <summary>
    /// Initializes the database.
    /// </summary>
    /// <param name="serviceScope">The service scope.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method is responsible for initializing the database. It is an abstract method, meaning it must be implemented in any non-abstract class that extends PlatformPersistenceModule.
    /// </remarks>
    public abstract Task InitializeDb(IServiceScope serviceScope);

    /// <summary>
    /// Registers the services related to the persistence layer of the platform.
    /// </summary>
    /// <param name="serviceCollection">The service collection to which the persistence services are registered.</param>
    /// <remarks>
    /// This method registers various services including the database context, unit of work, repositories, and persistence services.
    /// If the module is not only for cross database migration, it also registers the unit of work manager and event bus message repositories.
    /// </remarks>
    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformDbContext>(GetServicesRegisterScanAssemblies(), ServiceLifeTime.Scoped);

        if (!ForCrossDbMigrationOnly)
        {
            RegisterUnitOfWorkManager(serviceCollection);
            serviceCollection.RegisterAllFromType<IPlatformUnitOfWork>(GetServicesRegisterScanAssemblies());
            RegisterRepositories(serviceCollection);

            RegisterInboxEventBusMessageRepository(serviceCollection);
            RegisterOutboxEventBusMessageRepository(serviceCollection);

            serviceCollection.RegisterAllFromType<IPersistenceService>(GetServicesRegisterScanAssemblies());
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
            serviceCollection.RegisterAllFromType<IPlatformInboxBusMessageRepository>(GetServicesRegisterScanAssemblies());
    }

    protected virtual void RegisterOutboxEventBusMessageRepository(IServiceCollection serviceCollection)
    {
        if (EnableOutboxBusMessage())
            serviceCollection.RegisterAllFromType<IPlatformOutboxBusMessageRepository>(GetServicesRegisterScanAssemblies());
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
        serviceCollection.Register<IPlatformUnitOfWorkManager, PlatformDefaultPersistenceUnitOfWorkManager>(ServiceLifeTime.Scoped);

        serviceCollection.RegisterAllFromType<IPlatformUnitOfWorkManager>(
            GetServicesRegisterScanAssemblies(),
            ServiceLifeTime.Scoped,
            replaceIfExist: true,
            replaceStrategy: DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
    }

    private void RegisterRepositories(IServiceCollection serviceCollection)
    {
        if (ForCrossDbMigrationOnly) return;

        if (RegisterLimitedRepositoryImplementationTypes()?.Any() == true)
            RegisterLimitedRepositoryImplementationTypes()
                .ForEach(repositoryImplementationType => serviceCollection.RegisterAllForImplementation(repositoryImplementationType));
        else
            serviceCollection.RegisterAllFromType<IPlatformRepository>(GetServicesRegisterScanAssemblies());
    }
}

/// <inheritdoc cref="PlatformPersistenceModule" />
public abstract class PlatformPersistenceModule<TDbContext> : PlatformPersistenceModule, IPlatformPersistenceModule
    where TDbContext : class, IPlatformDbContext<TDbContext>
{
    protected PlatformPersistenceModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

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
            sleepDurationProvider: retryAttempt => DefaultDbInitAndMigrationRetryDelaySeconds.Seconds(),
            retryCount: DefaultDbInitAndMigrationRetryCount,
            onBeforeThrowFinalExceptionFn: exception => Logger.LogError(
                exception.BeautifyStackTrace(),
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
                exception.BeautifyStackTrace(),
                "[{DbContext}] {ExceptionType} detected on attempt InitializeDb",
                typeof(TDbContext).Name,
                exception.GetType().Name));
    }

    /// <summary>
    /// Override to config PooledDbContext. Default this feature is enabled by default true value of <see cref="PlatformPersistenceConfigurationPooledDbContextOptions.Enabled" />.
    /// When activated, pooled db context will be used for query/read cases
    /// </summary>
    public virtual PlatformPersistenceConfigurationPooledDbContextOptions PooledDbContextOption()
    {
        return new PlatformPersistenceConfigurationPooledDbContextOptions();
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllForImplementation<TDbContext>(ServiceLifeTime.Scoped);
        RegisterPersistenceConfiguration(serviceCollection);
        if (PooledDbContextOption().Enabled) RegisterDbContextPool(serviceCollection);

        base.InternalRegister(serviceCollection);
    }

    protected void RegisterPersistenceConfiguration(IServiceCollection serviceCollection)
    {
        serviceCollection.Register(
            sp => new PlatformPersistenceConfiguration<TDbContext>()
                .With(config => config.ForCrossDbMigrationOnly = ForCrossDbMigrationOnly)
                .With(config => config.PooledOptions = PooledDbContextOption())
                .Pipe(config => ConfigurePersistenceConfiguration(config, Configuration)),
            ServiceLifeTime.Singleton);
    }

    /// <summary>
    /// Configures the persistence configuration for the specific database context.
    /// </summary>
    /// <param name="config">The initial configuration of the persistence module.</param>
    /// <param name="configuration">The application's configuration.</param>
    /// <returns>The configured persistence configuration.</returns>
    protected virtual PlatformPersistenceConfiguration<TDbContext> ConfigurePersistenceConfiguration(
        PlatformPersistenceConfiguration<TDbContext> config,
        IConfiguration configuration)
    {
        return config;
    }

    protected virtual void RegisterDbContextPool(IServiceCollection serviceCollection)
    {
    }
}
