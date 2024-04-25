using System.Linq.Expressions;
using Easy.Platform.Application.Persistence;
using Easy.Platform.Common.Exceptions.Extensions;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions;
using Easy.Platform.Domain.Repositories;
using Easy.Platform.Domain.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Persistence.Domain;

public abstract class PlatformPersistenceRepository<TEntity, TPrimaryKey, TUow, TDbContext> : PlatformRepository<TEntity, TPrimaryKey, TUow>
    where TEntity : class, IEntity<TPrimaryKey>, new()
    where TUow : class, IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : IPlatformDbContext
{
    private readonly Lazy<ILogger> loggerLazy;

    protected PlatformPersistenceRepository(
        IPlatformUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider) : base(unitOfWorkManager, serviceProvider)
    {
        PersistenceConfiguration = serviceProvider.GetRequiredService<PlatformPersistenceConfiguration<TDbContext>>();
        loggerLazy = new Lazy<ILogger>(() => serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PlatformPersistenceRepository<,,,>)));
    }

    /// <summary>
    /// Return CurrentActiveUow db context if exist or. <br />
    /// Auto use GlobalUow if there's no current active uow. <br />
    /// Support for old system code or other application want to use platform repository inherit DbContext but without open new uow
    /// </summary>
    protected virtual TDbContext DbContext => GetUowDbContext(TryGetCurrentActiveUow() ?? UnitOfWorkManager.GlobalUow);

    protected PlatformPersistenceConfiguration<TDbContext> PersistenceConfiguration { get; }
    protected ILogger Logger => loggerLazy.Value;

    protected override async Task<TResult> ExecuteAutoOpenUowUsingOnceTimeForWrite<TResult>(
        Func<IPlatformUnitOfWork, Task<TResult>> action,
        IPlatformUnitOfWork forceUseUow = null)
    {
        return await ExecuteWithBadQueryWarningForWriteHandling(_ => base.ExecuteAutoOpenUowUsingOnceTimeForWrite(action, forceUseUow), null);
    }

    protected async Task<TResult> ExecuteWithBadQueryWarningForWriteHandling<TResult>(Func<IPlatformUnitOfWork, Task<TResult>> action, IPlatformUnitOfWork uow)
    {
        if (PersistenceConfiguration.BadQueryWarning.IsEnabled)
            return await IPlatformDbContext.ExecuteWithBadQueryWarningHandling<TResult, TEntity>(
                () => action(uow),
                Logger,
                PersistenceConfiguration,
                forWriteQuery: true,
                resultQuery: null,
                resultQueryStringBuilder: null);

        return await action(uow);
    }

    public TDbContext GetUowDbContext(IPlatformUnitOfWork uow)
    {
        return uow.UowOfType<TUow>().DbContext;
    }

    public abstract Task<List<TSource>> ToListAsync<TSource>(
        IEnumerable<TSource> source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Use ToAsyncEnumerable to convert IQueryable to IAsyncEnumerable to help return data like a stream. Also help
    /// using it as a true IEnumerable which Then can select anything and it will work.
    /// Default as Enumerable from IQueryable still like Queryable which cause error query could not be translated for free select using constructor map for example
    /// </summary>
    public abstract IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(
        IEnumerable<TSource> source,
        CancellationToken cancellationToken = default);

    public abstract Task<TSource> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public abstract Task<TSource> FirstAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public abstract Task<int> CountAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public abstract Task<bool> AnyAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public override Task<TEntity> GetByIdAsync(
        TPrimaryKey id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => FirstOrDefaultAsync(query.Where(p => p.Id!.Equals(id)), cancellationToken)
                .EnsureFound($"{typeof(TEntity).Name} with Id {id} is not found"),
            loadRelatedEntities);
    }

    public override Task<List<TEntity>> GetByIdsAsync(
        List<TPrimaryKey> ids,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => ToListAsync(query.Where(p => ids.Contains(p.Id)), cancellationToken),
            loadRelatedEntities);
    }

    public override Task<List<TEntity>> GetAllAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return ToListAsync(query, cancellationToken);
    }

    public override IEnumerable<TEntity> GetAllEnumerable(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (_, query) => ToAsyncEnumerable(query.WhereIf(predicate != null, predicate), cancellationToken).ToEnumerable(),
                loadRelatedEntities)
            .GetResult();
    }

    public override IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return ToAsyncEnumerable(query, cancellationToken);
    }

    public override IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (_, query) => ToAsyncEnumerable(queryBuilder(query), cancellationToken),
                loadRelatedEntities)
            .GetResult();
    }

    public override IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
                (uow, query) => ToAsyncEnumerable(queryBuilder(uow, query), cancellationToken),
                loadRelatedEntities)
            .GetResult();
    }

    public override Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => FirstOrDefaultAsync(query.WhereIf(predicate != null, predicate), cancellationToken)
                .EnsureFound($"{typeof(TEntity).Name} is not found"),
            loadRelatedEntities);
    }

    public override Task<TEntity> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => FirstOrDefaultAsync(query.WhereIf(predicate != null, predicate), cancellationToken),
            loadRelatedEntities);
    }

    public override Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => GetUowDbContext(uow).CountAsync(predicate, cancellationToken),
            []);
    }

    public override Task<int> CountAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default)
    {
        return CountAsync(query, cancellationToken);
    }

    public override Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => GetUowDbContext(uow).AnyAsync(predicate, cancellationToken),
            []);
    }

    public override Task<int> CountAsync<TQueryItemResult>(
        Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => CountAsync(queryBuilder(query), cancellationToken),
            []);
    }

    public override Task<int> CountAsync<TQueryItemResult>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => CountAsync(queryBuilder(uow, query), cancellationToken),
            []);
    }

    public override Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => ToListAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public override Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => ToListAsync(queryBuilder(uow, query), cancellationToken),
            loadRelatedEntities);
    }

    public override Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IQueryable<TEntity>, IEnumerable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => ToListAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public override Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IEnumerable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities)
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => ToListAsync(queryBuilder(uow, query), cancellationToken),
            loadRelatedEntities);
    }

    public override async Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities) where TSelector : default
    {
        return await ExecuteAutoOpenUowUsingOnceTimeForRead(
            (_, query) => FirstOrDefaultAsync(queryBuilder(query), cancellationToken),
            loadRelatedEntities);
    }

    public override Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IPlatformUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] loadRelatedEntities) where TSelector : default
    {
        return ExecuteAutoOpenUowUsingOnceTimeForRead(
            (uow, query) => FirstOrDefaultAsync(queryBuilder(uow, query), cancellationToken),
            loadRelatedEntities);
    }

    public override Task<TEntity> CreateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        return CreateAsync(null, entity, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public override async Task<TEntity> CreateAsync(
        IPlatformUnitOfWork uow,
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(CreateAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entity.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken),
                    uow);
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).CreateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken),
            uow);
    }

    public override async Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(CreateOrUpdateAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entity.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).CreateOrUpdateAsync<TEntity, TPrimaryKey>(entity, null, dismissSendEvent, eventCustomConfig, cancellationToken));
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).CreateOrUpdateAsync<TEntity, TPrimaryKey>(entity, null, dismissSendEvent, eventCustomConfig, cancellationToken));
    }

    public override async Task<TEntity> CreateOrUpdateAsync(
        TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(CreateOrUpdateAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entity.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow)
                        .CreateOrUpdateAsync<TEntity, TPrimaryKey>(entity, customCheckExistingPredicate, dismissSendEvent, eventCustomConfig, cancellationToken));
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow)
                .CreateOrUpdateAsync<TEntity, TPrimaryKey>(entity, customCheckExistingPredicate, dismissSendEvent, eventCustomConfig, cancellationToken));
    }

    public override async Task<List<TEntity>> CreateOrUpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        EnsureNoDuplicatedUniqueCompositeIdEntities(entities);

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(CreateOrUpdateManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entities.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow)
                        .CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
                            entities,
                            customCheckExistingPredicateBuilder,
                            dismissSendEvent,
                            eventCustomConfig,
                            cancellationToken));
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow)
                .CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
                    entities,
                    customCheckExistingPredicateBuilder,
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken));
    }

    public override async Task<List<TEntity>> CreateOrUpdateManyAsync(
        IPlatformUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        EnsureNoDuplicatedUniqueCompositeIdEntities(entities);

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(CreateOrUpdateManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entities.ToJson());

                return await ExecuteWithBadQueryWarningForWriteHandling(
                    uow => GetUowDbContext(uow)
                        .CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
                            entities,
                            customCheckExistingPredicateBuilder,
                            dismissSendEvent,
                            eventCustomConfig,
                            cancellationToken),
                    uow);
            }

        return await ExecuteWithBadQueryWarningForWriteHandling(
            uow => GetUowDbContext(uow)
                .CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
                    entities,
                    customCheckExistingPredicateBuilder,
                    dismissSendEvent,
                    eventCustomConfig,
                    cancellationToken),
            uow);
    }

    public override Task<TEntity> UpdateAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        return UpdateAsync(null, entity, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public override async Task<TEntity> UpdateAsync(
        IPlatformUnitOfWork uow,
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(UpdateAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entity.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken),
                    uow);
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).UpdateAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken),
            uow);
    }

    public override Task<TEntity> DeleteAsync(
        TPrimaryKey entityId,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        return DeleteAsync(null, entityId, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public override async Task<TEntity> DeleteAsync(
        IPlatformUnitOfWork uow,
        TPrimaryKey entityId,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(DeleteAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("EntityId", entityId);

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).DeleteAsync<TEntity, TPrimaryKey>(entityId, dismissSendEvent, eventCustomConfig, cancellationToken),
                    uow);
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteAsync<TEntity, TPrimaryKey>(entityId, dismissSendEvent, eventCustomConfig, cancellationToken),
            uow);
    }

    public override async Task<TEntity> DeleteAsync(
        TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(DeleteAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entity.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken));
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteAsync<TEntity, TPrimaryKey>(entity, dismissSendEvent, eventCustomConfig, cancellationToken));
    }

    public override async Task<List<TEntity>> CreateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        EnsureNoDuplicatedUniqueCompositeIdEntities(entities);

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(CreateManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entities.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).CreateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken));
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).CreateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken));
    }

    private void EnsureNoDuplicatedUniqueCompositeIdEntities(List<TEntity> entities)
    {
        if (entities.FirstOrDefault()?.As<IUniqueCompositeIdSupport<TEntity>>()?.UniqueCompositeId() == null) return;

        var duplicatedByCompositeIdEntityGroup = entities
            .Select(p => p.Cast<IUniqueCompositeIdSupport<TEntity>>())
            .GroupBy(p => p.UniqueCompositeId())
            .FirstOrDefault(p => p.Count() > 1);

        if (duplicatedByCompositeIdEntityGroup != null)
            throw new PlatformDomainException(
                $"DuplicatedCompositeId {duplicatedByCompositeIdEntityGroup.First().UniqueCompositeId()} to create/update {typeof(TEntity).Name} entity");
    }

    public override async Task<List<TEntity>> CreateManyAsync(
        IPlatformUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        EnsureNoDuplicatedUniqueCompositeIdEntities(entities);

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(CreateManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entities.ToJson());

                return await ExecuteWithBadQueryWarningForWriteHandling(
                    uow => GetUowDbContext(uow).CreateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken),
                    uow);
            }

        return await ExecuteWithBadQueryWarningForWriteHandling(
            uow => GetUowDbContext(uow).CreateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken),
            uow);
    }

    public override async Task<List<TEntity>> UpdateManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(UpdateManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entities.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).UpdateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken));
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).UpdateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken));
    }

    public override async Task<List<TEntity>> UpdateManyAsync(
        IPlatformUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(UpdateManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entities.ToJson());

                return await ExecuteWithBadQueryWarningForWriteHandling(
                    uow => GetUowDbContext(uow).UpdateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken),
                    uow);
            }

        return await ExecuteWithBadQueryWarningForWriteHandling(
            uow => GetUowDbContext(uow).UpdateManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken),
            uow);
    }

    public override async Task<List<TEntity>> DeleteManyAsync(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entityIds.IsEmpty()) return [];

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(DeleteManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entityIds.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(entityIds, dismissSendEvent, eventCustomConfig, cancellationToken));
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(entityIds, dismissSendEvent, eventCustomConfig, cancellationToken));
    }

    public override async Task<List<TEntity>> DeleteManyAsync(
        IPlatformUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(DeleteManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entities.ToJson());

                return await ExecuteWithBadQueryWarningForWriteHandling(
                    uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken),
                    uow);
            }

        return await ExecuteWithBadQueryWarningForWriteHandling(
            uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken),
            uow);
    }

    public override async Task<List<TEntity>> DeleteManyAsync(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (entities.IsEmpty()) return entities;

        if (IsDistributedTracingEnabled)
            using (var activity = IPlatformRepository.ActivitySource.StartActivity($"Repository.{nameof(DeleteManyAsync)}"))
            {
                activity?.AddTag("EntityType", typeof(TEntity).FullName);
                activity?.AddTag("Entity", entities.ToJson());

                return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
                    uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken));
            }

        return await ExecuteAutoOpenUowUsingOnceTimeForWrite(
            uow => GetUowDbContext(uow).DeleteManyAsync<TEntity, TPrimaryKey>(entities, dismissSendEvent, eventCustomConfig, cancellationToken));
    }
}

public abstract class PlatformPersistenceRootRepository<TEntity, TPrimaryKey, TUow, TDbContext>
    : PlatformPersistenceRepository<TEntity, TPrimaryKey, TUow, TDbContext>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
    where TUow : class, IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : IPlatformDbContext
{
    protected PlatformPersistenceRootRepository(IPlatformUnitOfWorkManager unitOfWorkManager, IServiceProvider serviceProvider) : base(
        unitOfWorkManager,
        serviceProvider)
    {
    }
}
