using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Domain.Repositories;
using Easy.Platform.MongoDB.Domain.UnitOfWork;
using Easy.Platform.MongoDB.Helpers;
using Easy.Platform.MongoDB.Mapping;
using Easy.Platform.MongoDB.Migration;
using Easy.Platform.MongoDB.Serializer.Abstract;
using Easy.Platform.MongoDB.Services;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using OpenTelemetry.Trace;

namespace Easy.Platform.MongoDB;

/// <summary>
///     <inheritdoc cref="PlatformPersistenceModule{TDbContext}" />
/// </summary>
public abstract class PlatformMongoDbPersistenceModule<TDbContext, TClientContext, TMongoOptions> : PlatformPersistenceModule<TDbContext>
    where TDbContext : PlatformMongoDbContext<TDbContext>
    where TClientContext : class, IPlatformMongoClient<TDbContext>
    where TMongoOptions : PlatformMongoOptions<TDbContext>
{
    public PlatformMongoDbPersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    public override Action<TracerProviderBuilder> AdditionalTracingConfigure => builder => builder.AddMongoDBInstrumentation();

    public static void RegisterClassMapType(Type platformMongoClassMapType)
    {
        if (PlatformMongoDbPersistenceModuleCache.RegisteredClassMapTypes.NotContains(platformMongoClassMapType))
        {
            Activator.CreateInstance(platformMongoClassMapType).As<IPlatformMongoClassMapping>().RegisterClassMap();
            PlatformMongoDbPersistenceModuleCache.RegisteredClassMapTypes.Add(platformMongoClassMapType);
        }
    }

    protected abstract void ConfigureMongoOptions(TMongoOptions options);

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.Configure<TMongoOptions>(ConfigureMongoOptions);
        serviceCollection.Configure<PlatformMongoOptions<TDbContext>>(
            options => ConfigureMongoOptions(Activator.CreateInstance<TMongoOptions>()));

        serviceCollection.RegisterAllForImplementation<TClientContext>(ServiceLifeTime.Singleton);
        serviceCollection.Register<IPlatformMongoClient<TDbContext>, TClientContext>(ServiceLifeTime.Singleton);

        serviceCollection.Register<PlatformMongoDbContext<TDbContext>, TDbContext>();

        if (!ForCrossDbMigrationOnly)
            RegisterMongoDbUow(serviceCollection);

        BsonClassMapHelper.TryRegisterClassMapWithDefaultInitializer<PlatformDataMigrationHistory>();
        BsonClassMapHelper.TryRegisterClassMapWithDefaultInitializer<PlatformMongoMigrationHistory>();
        AutoRegisterAllSerializers();
        AutoRegisterAllClassMap();
        RegisterBuiltInPersistenceServices(serviceCollection);
    }

    protected virtual void AutoRegisterAllClassMap()
    {
        AllClassMapTypes().ForEach(p => RegisterClassMapType(p));
    }

    protected virtual void AutoRegisterAllSerializers()
    {
        var allSerializerTypes = GetType()
            .Assembly.GetTypes()
            .Where(
                p => p.IsAssignableToGenericType(typeof(IPlatformMongoAutoRegisterBaseSerializer<>)) &&
                     p.IsClass &&
                     !p.IsAbstract)
            .ToList();
        var allBuiltInSerializerTypes = typeof(PlatformMongoDbPersistenceModule<>).Assembly.GetTypes()
            .Where(
                p => p.IsAssignableToGenericType(typeof(IPlatformMongoAutoRegisterBaseSerializer<>)) &&
                     p.IsClass &&
                     !p.IsAbstract)
            .ToList();

        allSerializerTypes.Concat(allBuiltInSerializerTypes)
            .ToList()
            .ForEach(
                p =>
                {
                    var serializerHandleValueType = p.GetInterfaces()
                        .First(
                            p => p.IsGenericType &&
                                 p.GetGenericTypeDefinition() == typeof(IPlatformMongoAutoRegisterBaseSerializer<>))
                        .GetGenericArguments()[0];

                    if (!PlatformMongoDbPersistenceModuleCache.RegisteredSerializerTypes.Contains(
                        serializerHandleValueType))
                    {
                        BsonSerializer.RegisterSerializer(
                            serializerHandleValueType,
                            (IPlatformMongoBaseSerializer)Activator.CreateInstance(p));

                        PlatformMongoDbPersistenceModuleCache.RegisteredSerializerTypes.Add(serializerHandleValueType);
                    }
                });
    }

    protected override void RegisterInboxEventBusMessageRepository(IServiceCollection serviceCollection)
    {
        if (!EnableInboxBusMessage())
            return;

        base.RegisterInboxEventBusMessageRepository(serviceCollection);

        // Register Default InboxBusMessageRepository if not existed custom inherited IPlatformInboxBusMessageRepository in assembly
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformInboxBusMessageRepository)))
            serviceCollection.Register(
                typeof(IPlatformInboxBusMessageRepository),
                typeof(PlatformDefaultMongoDbInboxBusMessageRepository<TDbContext>));

        // Register Default MongoInboxBusMessageClassMapping if not existed custom inherited PlatformMongoInboxBusMessageClassMapping in assembly
        if (!AllClassMapTypes().Any(p => p.IsAssignableTo(typeof(PlatformMongoInboxBusMessageClassMapping))))
            RegisterClassMapType(typeof(PlatformDefaultMongoInboxBusMessageClassMapping));
    }

    protected override void RegisterOutboxEventBusMessageRepository(IServiceCollection serviceCollection)
    {
        if (!EnableOutboxBusMessage())
            return;

        base.RegisterOutboxEventBusMessageRepository(serviceCollection);

        // Register Default OutboxEventBusMessageRepository if not existed custom inherited IPlatformOutboxEventBusMessageRepository in assembly
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformOutboxBusMessageRepository)))
            serviceCollection.Register(
                typeof(IPlatformOutboxBusMessageRepository),
                typeof(PlatformDefaultMongoDbOutboxBusMessageRepository<TDbContext>));

        // Register Default MongoOutboxBusMessageClassMapping if not existed custom inherited PlatformMongoOutboxBusMessageClassMapping in assembly
        if (!AllClassMapTypes().Any(p => p.IsAssignableTo(typeof(PlatformMongoOutboxBusMessageClassMapping))))
            RegisterClassMapType(typeof(PlatformDefaultMongoOutboxBusMessageClassMapping));
    }

    protected List<Type> AllClassMapTypes()
    {
        return GetType()
            .Assembly.GetTypes()
            .Where(p => p.IsAssignableTo(typeof(IPlatformMongoClassMapping)) && !p.IsAbstract && p.IsClass)
            .ToList();
    }

    private static void RegisterBuiltInPersistenceServices(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllForImplementation<MongoDbPlatformFullTextSearchPersistenceService>();
    }

    private void RegisterMongoDbUow(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformMongoDbPersistenceUnitOfWork<TDbContext>>(
            Assembly,
            ServiceLifeTime.Transient,
            replaceIfExist: true,
            DependencyInjectionExtension.ReplaceServiceStrategy.ByService);
        // Register default PlatformMongoDbUnitOfWork if not exist implementation in the concrete inherit persistence module
        if (serviceCollection.NotExist(p => p.ServiceType == typeof(IPlatformMongoDbPersistenceUnitOfWork<TDbContext>)))
            serviceCollection.RegisterAllForImplementation<PlatformMongoDbPersistenceUnitOfWork<TDbContext>>();

        serviceCollection.RegisterAllFromType<IUnitOfWork>(Assembly);
        // Register default PlatformMongoDbUnitOfWork for IUnitOfWork if not existing register for IUnitOfWork
        if (serviceCollection.NotExist(
            p => p.ServiceType == typeof(IUnitOfWork) &&
                 p.ImplementationType?.IsAssignableTo(typeof(IPlatformMongoDbPersistenceUnitOfWork<TDbContext>)) == true))
            serviceCollection.Register<IUnitOfWork, PlatformMongoDbPersistenceUnitOfWork<TDbContext>>();
    }
}

public abstract class PlatformMongoDbPersistenceModule<TDbContext, TClientContext>
    : PlatformMongoDbPersistenceModule<TDbContext, TClientContext, PlatformMongoOptions<TDbContext>>
    where TDbContext : PlatformMongoDbContext<TDbContext>
    where TClientContext : class, IPlatformMongoClient<TDbContext>
{
    protected PlatformMongoDbPersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }
}

public abstract class PlatformMongoDbPersistenceModule<TDbContext>
    : PlatformMongoDbPersistenceModule<TDbContext, PlatformMongoClient<TDbContext>>
    where TDbContext : PlatformMongoDbContext<TDbContext>
{
    protected PlatformMongoDbPersistenceModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }
}

/// <summary>
/// Could not store singleton cache in generic class because it will be singleton only for a specific generic type.
/// This class to serve singleton cache for PlatformMongoDbPersistenceModule
/// </summary>
public abstract class PlatformMongoDbPersistenceModuleCache
{
    public static readonly HashSet<Type> RegisteredClassMapTypes = new();
    public static readonly HashSet<Type> RegisteredSerializerTypes = new();
}
