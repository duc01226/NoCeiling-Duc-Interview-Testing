using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.EfCore.Domain.UnitOfWork;
using Easy.Platform.Persistence.Domain;
using Microsoft.EntityFrameworkCore;

namespace Easy.Platform.EfCore.Domain.Repositories;

public interface IPlatformEfCoreRepository
{
}

public abstract class PlatformEfCoreRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformPersistenceRepository<TEntity, TPrimaryKey, IPlatformEfCorePersistenceUnitOfWork<TDbContext>, TDbContext>, IPlatformEfCoreRepository
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreRepository(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        DbContextOptions<TDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        serviceProvider)
    {
        DbContextOptions = dbContextOptions;
    }

    protected DbContextOptions<TDbContext> DbContextOptions { get; }

    public virtual DbSet<TEntity> GetTable(IPlatformUnitOfWork uow)
    {
        return GetUowDbContext(uow).Set<TEntity>();
    }

    public override IQueryable<TEntity> GetQuery(IPlatformUnitOfWork uow, params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        // Note: apply .PipeIf(uow.IsUsingOnceTransientUow, query => query.AsNoTracking())
        // If EF Core finds an existing entity, then the same instance is returned, which can potentially use less memory and be faster than a no-tracking.
        // Actual after benchmark see that AsNoTracking ACTUALLY SLOWER. Still apply to fix case select OwnedEntities without parents work
        return GetTable(uow)
            .AsQueryable()
            .PipeIf(
                loadRelatedEntities.Any(),
                query => loadRelatedEntities.Aggregate(query, (query, loadRelatedEntityFn) => query.Include(loadRelatedEntityFn).DefaultIfEmpty()));
    }

    public override async Task<List<TSource>> ToListAsync<TSource>(
        IEnumerable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.As<IQueryable<TSource>>()?.ToListAsync(cancellationToken) ?? source.ToList().BoxedInTask(),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.As<IQueryable<TSource>>()
                    ?.Pipe(queryable => queryable != null ? queryable.ToQueryString : (Func<string>)null));

        return await (source.As<IQueryable<TSource>>()?.ToListAsync(cancellationToken) ?? source.ToList().BoxedInTask());
    }

    public override IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(IEnumerable<TSource> source, CancellationToken cancellationToken = default)
    {
        return source.As<IQueryable<TSource>>()?.AsAsyncEnumerable() ?? source.ToAsyncEnumerable();
    }

    public override async Task<TSource> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.FirstOrDefaultAsync(cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.As<IQueryable<TSource>>()
                    ?.Pipe(queryable => queryable != null ? queryable.ToQueryString : (Func<string>)null));

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
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.FirstAsync(cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.As<IQueryable<TSource>>()
                    ?.Pipe(queryable => queryable != null ? queryable.ToQueryString : (Func<string>)null));

        return await source.FirstAsync(cancellationToken);
    }

    public override async Task<int> CountAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.CountAsync(cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.As<IQueryable<TSource>>()
                    ?.Pipe(queryable => queryable != null ? queryable.ToQueryString : (Func<string>)null));

        return await source.CountAsync(cancellationToken);
    }

    public override async Task<bool> AnyAsync<TSource>(IQueryable<TSource> source, CancellationToken cancellationToken = default)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling(
                () => source.AnyAsync(cancellationToken),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: false,
                resultQuery: source,
                resultQueryStringBuilder: source.As<IQueryable<TSource>>()
                    ?.Pipe(queryable => queryable != null ? queryable.ToQueryString : (Func<string>)null));

        return await source.AnyAsync(cancellationToken);
    }

    protected override void HandleDisposeUsingOnceTransientUowLogic<TResult>(
        IPlatformUnitOfWork uow,
        Expression<Func<TEntity, object>>[] loadRelatedEntities,
        TResult result)
    {
        var canDisposeContext = !DoesNeedKeepUowForQueryOrEnumerableExecutionLater(result, uow);

        if (DbContextOptions.IsUsingLazyLoadingProxy() && loadRelatedEntities?.Any() == true && canDisposeContext)
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

        if (canDisposeContext)
            uow.Dispose();
    }

    // If result has entity instance and MustKeepUowForQuery == true => ef core might use lazy-loading => need to keep the uow for db context
    // to help the entity could load lazy navigation property. If uow disposed => context disposed => lazy-loading proxy failed because db-context disposed
    protected override bool DoesNeedKeepUowForQueryOrEnumerableExecutionLater<TResult>(TResult result, IPlatformUnitOfWork uow)
    {
        if (result == null ||
            result.GetType().Pipe(p => p.IsPrimitive || p.IsValueType) ||
            result is string ||
            result.As<ICollection>()?.Count == 0)
            return false;

        if (result.GetType()
            .Pipe(
                resultType => IsEnumerableExecutionLaterType(resultType) ||
                              resultType.GetInterfaces()
                                  .FirstOrDefault(p => p.IsAssignableToGenericType(typeof(IDictionary<,>)))
                                  .Pipe(p => p != null && IsEnumerableExecutionLaterType(p.GenericTypeArguments[1]))))
            return true;

        // Keep uow for lazy-loading if the result is entity, Dictionary or Grouped result of entity or list entities
        return DbContextOptions.IsUsingLazyLoadingProxy() &&
               (IsEntityOrListEntity(result) || IsDictionaryOfValueOfEntityOrListEntity(result.GetType()));

        static bool IsEntityOrListEntity<TData>(TData data)
        {
            var result = data is IEntity ||
                         data.GetType()
                             .Pipe(
                                 p => p.GetInterfaces()
                                          .FirstOrDefault(p => p.IsAssignableToGenericType(typeof(IEnumerable<>)))
                                          .Pipe(p => p != null && p.GenericTypeArguments[0].IsAssignableTo(typeof(IEntity))) ||
                                      p.GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(p => p.PropertyType.IsAssignableTo(typeof(IEntity))));

            return result;
        }

        bool IsDictionaryOfValueOfEntityOrListEntity(Type resultType)
        {
            return resultType
                .GetInterfaces()
                .FirstOrDefault(p => p.IsAssignableToGenericType(typeof(IDictionary<,>)))
                .Pipe(p => p != null && IsEntityOrListEntity(p.GenericTypeArguments[1]));
        }

        bool IsEnumerableExecutionLaterType(Type resultType)
        {
            return resultType.IsAssignableToGenericType(typeof(IQueryable<>)) ||
                   resultType.IsAssignableToGenericType(typeof(IAsyncEnumerable<>)) ||
                   (resultType.IsAssignableToGenericType(typeof(IEnumerable<>)) && !resultType.IsAssignableToGenericType(typeof(ICollection<>)));
        }
    }
}

public abstract class PlatformEfCoreRootRepository<TEntity, TPrimaryKey, TDbContext>
    : PlatformEfCoreRepository<TEntity, TPrimaryKey, TDbContext>, IPlatformRootRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
    where TDbContext : PlatformEfCoreDbContext<TDbContext>
{
    public PlatformEfCoreRootRepository(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        DbContextOptions<TDbContext> dbContextOptions,
        IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        dbContextOptions,
        serviceProvider)
    {
    }
}
