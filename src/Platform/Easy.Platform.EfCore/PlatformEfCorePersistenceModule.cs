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

namespace Easy.Platform.EfCore;

/// <summary>
/// <inheritdoc cref="PlatformPersistenceModule{TDbContext}"/>
/// </summary>
public abstract class PlatformEfCorePersistenceModule<TDbContext> : PlatformPersistenceModule<TDbContext>
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCorePersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        RegisterDbContextOptions(serviceCollection);
        RegisterEfCoreUow(serviceCollection);

        serviceCollection.Register<IPlatformFullTextSearchPersistenceService>(FullTextSearchPersistenceServiceProvider);
    }

    /// <summary>
    /// Return a action for <see cref="DbContextOptionsBuilder"/> to AddDbContext. <br/>
    /// Example: return options => options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
    /// </summary>
    protected abstract Action<DbContextOptionsBuilder> DbContextOptionsBuilderActionProvider(IServiceProvider serviceProvider);

    /// <summary>
    /// Default return <see cref="LikeOperationEfCorePlatformFullTextSearchPersistenceService"/>
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
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformInboxBusMessageRepository)))
            serviceCollection.Register(
                typeof(IPlatformInboxBusMessageRepository),
                typeof(PlatformDefaultEfCoreInboxBusMessageRepository<TDbContext>),
                ServiceLifeTime.Transient);
    }

    protected override void RegisterOutboxEventBusMessageRepository(IServiceCollection serviceCollection)
    {
        if (!EnableOutboxBusMessage())
            return;

        base.RegisterOutboxEventBusMessageRepository(serviceCollection);

        // Register Default OutboxEventBusMessageRepository if not existed custom inherited IPlatformOutboxEventBusMessageRepository in assembly
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformOutboxBusMessageRepository)))
            serviceCollection.Register<IPlatformOutboxBusMessageRepository, PlatformDefaultEfCoreOutboxBusMessageRepository<TDbContext>>();
    }

    private void RegisterDbContextOptions(IServiceCollection serviceCollection)
    {
        serviceCollection.Register(sp => CreateDbContextOptions(sp));

        serviceCollection.Register<DbContextOptions, DbContextOptions<TDbContext>>();
    }

    private void RegisterEfCoreUow(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformEfCoreUnitOfWork<TDbContext>>(
            Assembly,
            ServiceLifeTime.Transient,
            replaceIfExist: true,
            DependencyInjectionExtension.ReplaceServiceStrategy.ByService);
        // Register default PlatformMongoDbUnitOfWork if not any implementation in the concrete inherit persistence module
        if (serviceCollection.NotExist(p => p.ServiceType == typeof(IPlatformEfCoreUnitOfWork<TDbContext>)))
            serviceCollection.RegisterAllForImplementation<PlatformEfCoreUnitOfWork<TDbContext>>();

        serviceCollection.RegisterAllFromType<IUnitOfWork>(Assembly);
        // Register default PlatformEfCoreUnitOfWork for IUnitOfWork if not existing register for IUnitOfWork
        if (serviceCollection.NotExist(
            p => p.ServiceType == typeof(IUnitOfWork) &&
                 p.ImplementationType?.IsAssignableTo(typeof(IPlatformEfCoreUnitOfWork<TDbContext>)) == true))
            serviceCollection.Register<IUnitOfWork, PlatformEfCoreUnitOfWork<TDbContext>>();
    }

    private DbContextOptions<TDbContext> CreateDbContextOptions(
        IServiceProvider serviceProvider)
    {
        var builder = new DbContextOptionsBuilder<TDbContext>(
            new DbContextOptions<TDbContext>(new Dictionary<Type, IDbContextOptionsExtension>()));

        builder.UseApplicationServiceProvider(serviceProvider);

        DbContextOptionsBuilderActionProvider(serviceProvider).Invoke(builder);

        return builder.Options;
    }
}
