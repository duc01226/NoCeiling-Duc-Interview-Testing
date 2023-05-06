using System.Linq.Expressions;
using System.Reflection;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.ValueObjects.Abstract;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.Domain.UnitOfWork;
using Easy.Platform.Persistence.Domain;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.Repositories;

public abstract class PlatformEfCoreRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformPersistenceRepository<TEntity, TPrimaryKey, IPlatformEfCorePersistenceUnitOfWork<TDbContext>, TDbContext>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreRepository(
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        DbContextOptions<TDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        serviceProvider)
    {
        DbContextOptions = dbContextOptions;
        AllAvailableEntityTypes = typeof(TEntity).Assembly.GetTypes().Where(p => p.IsClass && !p.IsAbstract && p.IsAssignableTo(typeof(IEntity))).ToHashSet();
        ToCheckNoNeedKeepUowPrimitiveTypes = new[]
        {
            typeof(string),
            typeof(Guid),
            typeof(DateTime),
            typeof(int),
            typeof(long),
            typeof(double),
            typeof(float),
            typeof(DateOnly)
        };
    }

    protected DbContextOptions<TDbContext> DbContextOptions { get; }

    protected HashSet<Type> AllAvailableEntityTypes { get; }

    protected Type[] ToCheckNoNeedKeepUowPrimitiveTypes { get; }

    protected virtual bool IsDefaultNoTrackingQuery => true;

    public virtual DbSet<TEntity> Table => DbContext.Set<TEntity>();

    public virtual DbSet<TEntity> GetTable(IUnitOfWork uow)
    {
        return GetUowDbContext(uow).Set<TEntity>();
    }

    public override IQueryable<TEntity> GetQuery(IUnitOfWork uow, params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return GetTable(uow)
            .PipeIf(IsDefaultNoTrackingQuery && !DbContextOptions.IsUsingLazyLoadingProxy(), p => p.AsNoTracking())
            .AsQueryable()
            .PipeIf(
                loadRelatedEntities.Any(),
                query => loadRelatedEntities.Aggregate(query, (query, loadRelatedEntityFn) => query.Include(loadRelatedEntityFn)));
    }

    public override async Task<List<TSource>> ToListAsync<TSource>(
        IEnumerable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                async () => await (source.As<IQueryable<TSource>>()?.ToListAsync(cancellationToken) ?? source.ToList().ToTask()),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                source);

        return await (source.As<IQueryable<TSource>>()?.ToListAsync(cancellationToken) ?? source.ToList().ToTask());
    }

    public override IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(IEnumerable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.As<IQueryable<TSource>>()?.AsAsyncEnumerable() ?? source.ToAsyncEnumerable();
    }

    public override async Task<TSource> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return await source.FirstOrDefaultAsync(cancellationToken);
    }

    public override async Task<TSource> FirstOrDefaultAsync<TSource>(
        IEnumerable<TSource> query,
        CancellationToken cancellationToken = default)
    {
        if (query.As<IQueryable<TSource>>() != null)
            return await FirstOrDefaultAsync(query.As<IQueryable<TSource>>(), cancellationToken);

        return query.FirstOrDefault();
    }

    public override async Task<TSource> FirstAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        return await source.FirstAsync(cancellationToken);
    }

    public override async Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return await source.CountAsync(cancellationToken);
    }

    public override async Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        return await source.AnyAsync(cancellationToken);
    }

    protected override void HandleDisposeUsingOnceTimeContextLogic<TResult>(
        IUnitOfWork uow,
        bool doesNeedKeepUowForQueryOrEnumerableExecutionLater,
        Expression<Func<TEntity, object>>[]? loadRelatedEntities,
        TResult result)
    {
        var needDisposeContext = !doesNeedKeepUowForQueryOrEnumerableExecutionLater;

        if (loadRelatedEntities?.Any() == true && needDisposeContext && DbContextOptions.IsUsingLazyLoadingProxy())
        {
            // Fix Eager loading include with using UseLazyLoadingProxies of EfCore by try to access the entity before dispose context
            if (result is TEntity entity)
                loadRelatedEntities.ForEach(loadRelatedEntityFn => loadRelatedEntityFn.Compile()(entity));
            else if (result is IEnumerable<TEntity> entities)
                entities.ForEach(entity => loadRelatedEntities.ForEach(loadRelatedEntityFn => loadRelatedEntityFn.Compile()(entity)));
            else
                result?.GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.PropertyType == typeof(TEntity))
                    .ForEach(
                        entityPropertyInfo => loadRelatedEntities
                            .ForEach(loadRelatedEntityFn => loadRelatedEntityFn.Compile()(entityPropertyInfo.GetValue(result).As<TEntity>())));
        }

        if (needDisposeContext) uow.Dispose();
    }

    // If result has entity instance and MustKeepUowForQuery == true => ef core might use lazy-loading => need to keep the uow for db context
    // to help the entity could load lazy navigation property. If uow disposed => context disposed => lazy-loading proxy failed because db-context disposed
    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IUnitOfWork uow)
    {
        if (result == null) return false;

        if (result.GetType().IsAssignableToGenericType(typeof(IQueryable<>)) ||
            result.GetType().IsAssignableToGenericType(typeof(IAsyncEnumerable<>))) return true;

        var matchedDbContextUow = uow.UowOfType<IPlatformEfCorePersistenceUnitOfWork<TDbContext>>();
        if (matchedDbContextUow.MustKeepUowForQuery() == false || matchedDbContextUow.IsPseudoTransactionUow())
            return false;

        // Not need to keep uow for lazy-loading If the result is primitive-type/value-object or Enumerable of primitive type/value-object
        if (IsPrimitiveOrValueObjectType(result) ||
            (result.GetType().IsAssignableToGenericType(typeof(IEnumerable<>)) &&
             ToCheckNoNeedKeepUowPrimitiveTypes.Any(primitiveType => result.GetType().IsAssignableTo(typeof(IEnumerable<>).MakeGenericType(primitiveType)))))
            return false;

        // Keep uow for lazy-loading if the result is entity, Dictionary or Grouped result of entity or list entities
        return IsEntityOrListEntity(result) ||
               result.As<IDictionary<string, object>>()?.Any(p => IsEntityOrListEntity(p.Value)) == true ||
               IsDictionaryOfValueOfEntityOrListEntity(result, ToCheckNoNeedKeepUowPrimitiveTypes, AllAvailableEntityTypes);

        static bool IsPrimitiveOrValueObjectType<TData>(TData data)
        {
            var result = data is string ||
                         data.GetType().IsValueType ||
                         data is IPlatformValueObject;

            return result;
        }

        static bool IsEntityOrListEntity<TData>(TData data)
        {
            var result = data is IEntity ||
                         data.As<IEnumerable<IEntity>>()?.Any() == true ||
                         data.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(p => p.PropertyType.IsAssignableTo(typeof(IEntity)));

            return result;
        }

        static bool IsDictionaryOfValueOfEntityOrListEntity(TResult data, Type[] allAvailableEntityDictionaryKeyTypes, HashSet<Type> allAvailableEntityTypes)
        {
            if (!data.GetType().IsAssignableToGenericType(typeof(IDictionary<,>)))
                return false;

            var result = allAvailableEntityDictionaryKeyTypes.Any(
                keyType => allAvailableEntityTypes.Any(
                    entityType => IsDictionaryOfValueOfEntityOrListEntity(data, keyType, typeof(TEntity)) ||
                                  IsDictionaryOfValueOfEntityOrListEntity(data, keyType, entityType)));

            return result;

            static bool IsDictionaryOfValueOfEntityOrListEntity(TResult result, Type keyType, Type entityType)
            {
                return result.GetType().IsAssignableTo(typeof(IDictionary<,>).MakeGenericType(keyType, entityType)) ||
                       result.GetType().IsAssignableTo(typeof(IDictionary<,>).MakeGenericType(keyType, typeof(IEnumerable<>).MakeGenericType(entityType))) ||
                       result.GetType().IsAssignableTo(typeof(IDictionary<,>).MakeGenericType(keyType, typeof(List<>).MakeGenericType(entityType))) ||
                       result.GetType().IsAssignableTo(typeof(IDictionary<,>).MakeGenericType(keyType, typeof(HashSet<>).MakeGenericType(entityType))) ||
                       result.GetType().IsAssignableTo(typeof(IDictionary<,>).MakeGenericType(keyType, typeof(ICollection<>).MakeGenericType(entityType)));
            }
        }
    }
}

public abstract class PlatformEfCoreRootRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformEfCoreRepository<TEntity, TPrimaryKey, TDbContext>, IPlatformRootRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreRootRepository(
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        DbContextOptions<TDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        cqrs,
        dbContextOptions,
        serviceProvider)
    {
    }
}
