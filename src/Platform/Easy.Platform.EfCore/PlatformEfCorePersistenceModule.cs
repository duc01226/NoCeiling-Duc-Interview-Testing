using System.Diagnostics;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.Domain.Repositories;
using Easy.Platform.EfCore.Domain.UnitOfWork;
using Easy.Platform.EfCore.Services;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Trace;

namespace Easy.Platform.EfCore;

/// <summary>
///     <inheritdoc cref="PlatformPersistenceModule{TDbContext}" />
/// </summary>
public abstract class PlatformEfCorePersistenceModule<TDbContext> : PlatformPersistenceModule<TDbContext>
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCorePersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    public override Action<TracerProviderBuilder> AdditionalTracingConfigure =>
        builder => builder
            .AddSqlClientInstrumentation(options => options.SetDbStatementForText = true)
            .AddNpgsql();

    /// <summary>
    /// If true enable show query to Debug output
    /// </summary>
    public virtual bool EnableDebugQueryLog { get; set; } = true;

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        RegisterDbContextOptions(serviceCollection);

        if (!ForCrossDbMigrationOnly) RegisterEfCoreUow(serviceCollection);

        if (!ForCrossDbMigrationOnly || serviceCollection.All(p => p.ServiceType != typeof(IPlatformFullTextSearchPersistenceService)))
            serviceCollection.Register<IPlatformFullTextSearchPersistenceService>(FullTextSearchPersistenceServiceProvider);
    }

    protected override void RegisterDbContextPool(IServiceCollection serviceCollection)
    {
        var options = PooledDbContextOption();

        serviceCollection.Register(
            sp => new PooledDbContextFactory<TDbContext>(sp.GetRequiredService<DbContextOptions<TDbContext>>(), options.PoolSize),
            ServiceLifeTime.Singleton);
        serviceCollection.AddDbContextPool<TDbContext>(
            o => ConfigureDbContextOptionsBuilder(serviceCollection.BuildServiceProvider(), o),
            options.PoolSize);
    }

    /// <summary>
    /// Return a action for <see cref="DbContextOptionsBuilder" /> to AddDbContext. <br />
    /// Example: return options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
    /// </summary>
    protected abstract Action<DbContextOptionsBuilder> DbContextOptionsBuilderActionProvider(IServiceProvider serviceProvider);

    /// <summary>
    /// Default return <see cref="LikeOperationEfCorePlatformFullTextSearchPersistenceService" />
    /// Override the default instance with new class to NOT USE DEFAULT LIKE OPERATION FOR BETTER PERFORMANCE
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <returns></returns>
    protected virtual EfCorePlatformFullTextSearchPersistenceService FullTextSearchPersistenceServiceProvider(IServiceProvider serviceProvider)
    {
        return new LikeOperationEfCorePlatformFullTextSearchPersistenceService(serviceProvider);
    }

    protected override void RegisterInboxEventBusMessageRepository(IServiceCollection serviceCollection)
    {
        if (!EnableInboxBusMessage())
            return;

        base.RegisterInboxEventBusMessageRepository(serviceCollection);

        // Register Default InboxEventBusMessageRepository if not existed custom inherited IPlatformInboxEventBusMessageRepository in assembly
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformInboxBusMessageRepository<TDbContext>)))
        {
            serviceCollection.RegisterAllForImplementation<PlatformDefaultEfCoreInboxBusMessageRepository<TDbContext>>();
            serviceCollection.Register<IPlatformInboxBusMessageRepository, PlatformDefaultEfCoreInboxBusMessageRepository<TDbContext>>();
        }
    }

    protected override void RegisterOutboxEventBusMessageRepository(IServiceCollection serviceCollection)
    {
        if (!EnableOutboxBusMessage())
            return;

        base.RegisterOutboxEventBusMessageRepository(serviceCollection);

        // Register Default OutboxEventBusMessageRepository if not existed custom inherited IPlatformOutboxEventBusMessageRepository in assembly
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformOutboxBusMessageRepository<TDbContext>)))
        {
            serviceCollection.RegisterAllForImplementation<PlatformDefaultEfCoreOutboxBusMessageRepository<TDbContext>>();
            serviceCollection.Register<IPlatformOutboxBusMessageRepository, PlatformDefaultEfCoreOutboxBusMessageRepository<TDbContext>>();
        }
    }

    private void RegisterDbContextOptions(IServiceCollection serviceCollection)
    {
        serviceCollection.Register(CreateDbContextOptions, ServiceLifeTime.Singleton);
    }

    private void RegisterEfCoreUow(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformEfCorePersistenceUnitOfWork<TDbContext>>(GetServicesRegisterScanAssemblies());
        // Register default PlatformMongoDbUnitOfWork if not any implementation in the concrete inherit persistence module
        if (serviceCollection.NotExist(p => p.ServiceType == typeof(IPlatformEfCorePersistenceUnitOfWork<TDbContext>)))
            serviceCollection.RegisterAllForImplementation<PlatformEfCorePersistenceUnitOfWork<TDbContext>>();

        serviceCollection.RegisterAllFromType<IPlatformUnitOfWork>(GetServicesRegisterScanAssemblies());
        // Register default PlatformEfCoreUnitOfWork for IUnitOfWork if not existing register for IUnitOfWork
        if (serviceCollection.NotExist(
            p => p.ServiceType == typeof(IPlatformUnitOfWork) &&
                 p.ImplementationType?.IsAssignableTo(typeof(IPlatformEfCorePersistenceUnitOfWork<TDbContext>)) == true))
            serviceCollection.Register<IPlatformUnitOfWork, PlatformEfCorePersistenceUnitOfWork<TDbContext>>();
    }

    private DbContextOptions<TDbContext> CreateDbContextOptions(
        IServiceProvider serviceProvider)
    {
        var builder = new DbContextOptionsBuilder<TDbContext>(
            new DbContextOptions<TDbContext>(new Dictionary<Type, IDbContextOptionsExtension>()));

        ConfigureDbContextOptionsBuilder(serviceProvider, builder);

        return builder.Options;
    }

    private void ConfigureDbContextOptionsBuilder(IServiceProvider serviceProvider, DbContextOptionsBuilder builder)
    {
        builder.UseApplicationServiceProvider(serviceProvider);

        DbContextOptionsBuilderActionProvider(serviceProvider).Invoke(builder);

        if (Debugger.IsAttached && EnableDebugQueryLog) builder.UseLoggerFactory(Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddDebug()));
    }
}
