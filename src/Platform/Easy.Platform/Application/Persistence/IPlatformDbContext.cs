#nullable enable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Easy.Platform.Domain.Entities;
using Easy.Platform.Domain.Events;
using Easy.Platform.Domain.Exceptions.Extensions;
using Easy.Platform.Domain.UnitOfWork;
using Easy.Platform.Persistence;
using Easy.Platform.Persistence.DataMigration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Application.Persistence;

public interface IPlatformDbContext : IDisposable
{
    public const int DefaultPageSize = 10;
    public const int DefaultRunDataMigrationInBackgroundRetryCount = 10;

    public IQueryable<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryQuery { get; }

    public IUnitOfWork MappedUnitOfWork { get; set; }

    public ILogger Logger { get; }

    public Task InsertOneDataMigrationHistoryAsync(PlatformDataMigrationHistory entity);

    public async Task MigrateApplicationDataAsync<TDbContext>(IServiceProvider serviceProvider) where TDbContext : class, IPlatformDbContext<TDbContext>
    {
        PlatformDataMigrationExecutor<TDbContext>
            .EnsureAllDataMigrationExecutorsHasUniqueName(GetType().Assembly, serviceProvider);

        await PlatformDataMigrationExecutor<TDbContext>
            .GetCanExecuteDataMigrationExecutors(GetType().Assembly, serviceProvider, ApplicationDataMigrationHistoryQuery)
            .ForEachAsync(
                async migrationExecution =>
                {
                    try
                    {
                        if (migrationExecution.AllowRunInBackgroundThread)
                            ExecuteDataMigrationInBackgroundThread(migrationExecution, GetType(), Logger);
                        else
                            await ExecuteDataMigrationExecutor(migrationExecution);

                        migrationExecution.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogDataMigrationFailedError(Logger, ex, migrationExecution.Name);

                        throw;
                    }
                });
    }

    public static void ExecuteDataMigrationInBackgroundThread<TDbContext>(
        PlatformDataMigrationExecutor<TDbContext> migrationExecution,
        Type dbContextType,
        ILogger logger) where TDbContext : class, IPlatformDbContext<TDbContext>
    {
        var migrationExecutionType = migrationExecution.GetType();
        var migrationExecutionName = migrationExecution.Name;

        logger.LogInformation("Wait To Execute DataMigration {MigrationExecutionName} in background thread", migrationExecutionName);

        Util.TaskRunner.QueueActionInBackground(
            async () =>
            {
                var currentDbContextPersistenceModule = PlatformGlobal.ServiceProvider
                    .GetRequiredService(dbContextType.Assembly.GetTypes().First(p => p.IsAssignableTo(typeof(PlatformPersistenceModule<TDbContext>))))
                    .As<PlatformPersistenceModule<TDbContext>>();

                try
                {
                    await currentDbContextPersistenceModule.BackgroundThreadDataMigrationLock.WaitAsync();

                    await ExecuteDataMigrationExecutorInNewScope<TDbContext>(
                        migrationExecutionName,
                        migrationExecutionType,
                        () => PlatformGlobal.LoggerFactory.CreateLogger(dbContextType));
                }
                finally
                {
                    currentDbContextPersistenceModule.BackgroundThreadDataMigrationLock.Release();
                }
            },
            () => PlatformGlobal.LoggerFactory.CreateLogger(dbContextType));
    }

    public static void LogDataMigrationFailedError(ILogger logger, Exception ex, string migrationExecutionName)
    {
        logger.LogError(
            ex,
            "DataMigration {DataMigrationName} FAILED. [Error:{Error}].",
            migrationExecutionName,
            ex.Message);
    }

    public static async Task ExecuteDataMigrationExecutorInNewScope<TDbContext>(
        string migrationExecutionName,
        Type migrationExecutionType,
        Func<ILogger> loggerFactory)
        where TDbContext : class, IPlatformDbContext<TDbContext>
    {
        try
        {
            await PlatformGlobal.ServiceProvider.ExecuteInjectScopedAsync(
                async (IServiceProvider sp) =>
                {
                    var dbContext = sp.GetRequiredService(typeof(TDbContext)).As<TDbContext>();

                    await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                        () => PlatformGlobal.ServiceProvider.ExecuteInjectScopedAsync(
                            async (IServiceProvider sp) =>
                            {
                                await dbContext.ExecuteDataMigrationExecutor(
                                    PlatformDataMigrationExecutor<TDbContext>.CreateNewInstance(sp, migrationExecutionType));
                            }),
                        retryCount: DefaultRunDataMigrationInBackgroundRetryCount,
                        sleepDurationProvider: retryAttempt => 10.Seconds(),
                        onRetry: (ex, timeSpan, currentRetry, ctx) =>
                        {
                            dbContext.Logger.LogWarning(
                                ex,
                                "Retry Execute DataMigration {MigrationExecutionType.Name} {CurrentRetry} time(s).",
                                migrationExecutionType.Name,
                                currentRetry);
                        });
                });
        }
        catch (Exception ex)
        {
            LogDataMigrationFailedError(loggerFactory(), ex, migrationExecutionName);
        }
    }

    public async Task ExecuteDataMigrationExecutor<TDbContext>(PlatformDataMigrationExecutor<TDbContext> migrationExecution)
        where TDbContext : class, IPlatformDbContext
    {
        Logger.LogInformation("DataMigration {MigrationExecutionName} STARTED.", migrationExecution.Name);

        await migrationExecution.Execute(this.As<TDbContext>());

        await InsertOneDataMigrationHistoryAsync(new PlatformDataMigrationHistory(migrationExecution.Name));

        await this.As<TDbContext>().SaveChangesAsync();

        this.As<TDbContext>().Logger.LogInformation("DataMigration {MigrationExecutionName} FINISHED.", migrationExecution.Name);
    }

    public static async Task<TResult> ExecuteWithBadQueryWarningHandling<TResult, TSource>(
        Func<Task<TResult>> getResultFn,
        ILogger logger,
        IPlatformPersistenceConfiguration persistenceConfiguration,
        bool forWriteQuery,
        IEnumerable<TSource>? resultQuery,
        Func<string>? resultQueryStringBuilder)
    {
        // Must use stack trace BEFORE await fn() BECAUSE after call get data function, the stack trace get lost because
        // some unknown reason (ToListAsync, FirstOrDefault, XXAsync from ef-core, mongo-db). Could be the thread/task context has been changed
        // after get data from database, it switched to I/O thread pool
        var loggingFullStackTrace = Environment.StackTrace;

        if (typeof(TResult).IsAssignableToGenericType(typeof(IEnumerable<>)) && resultQuery != null)
            HandleLogTooMuchDataInMemoryBadQueryWarning(resultQuery, persistenceConfiguration, logger, loggingFullStackTrace, resultQueryStringBuilder);

        var result = await HandleLogSlowQueryBadQueryWarning(
            getResultFn,
            persistenceConfiguration,
            logger,
            loggingFullStackTrace,
            forWriteQuery,
            resultQueryStringBuilder);

        return result;

        static void HandleLogTooMuchDataInMemoryBadQueryWarning(
            [AllowNull] IEnumerable<TSource> resultQuery,
            IPlatformPersistenceConfiguration persistenceConfiguration,
            ILogger logger,
            string loggingFullStackTrace,
            [AllowNull] Func<string> resultQueryStringBuilder)
        {
            var queryResultCount = resultQuery?.Count() ?? 0;

            if (queryResultCount >= persistenceConfiguration.BadQueryWarning.TotalItemsThreshold)
                LogTooMuchDataInMemoryBadQueryWarning(queryResultCount, logger, persistenceConfiguration, loggingFullStackTrace, resultQueryStringBuilder);
        }

        static async Task<TResult> HandleLogSlowQueryBadQueryWarning(
            Func<Task<TResult>> getResultFn,
            IPlatformPersistenceConfiguration persistenceConfiguration,
            ILogger logger,
            string loggingFullStackTrace,
            bool forWriteQuery,
            [AllowNull] Func<string> resultQueryStringBuilder)
        {
            var startQueryTimeStamp = Stopwatch.GetTimestamp();

            var result = await getResultFn();

            var queryElapsedTime = Stopwatch.GetElapsedTime(startQueryTimeStamp);

            if (queryElapsedTime.TotalMilliseconds >= persistenceConfiguration.BadQueryWarning.GetSlowQueryMillisecondsThreshold(forWriteQuery))
                LogSlowQueryBadQueryWarning(queryElapsedTime, logger, persistenceConfiguration, loggingFullStackTrace, resultQueryStringBuilder);
            return result;
        }
    }

    public static void LogSlowQueryBadQueryWarning(
        TimeSpan queryElapsedTime,
        ILogger logger,
        IPlatformPersistenceConfiguration persistenceConfiguration,
        string loggingStackTrace,
        [AllowNull] Func<string> resultQueryStringBuilder)
    {
        logger.Log(
            persistenceConfiguration.BadQueryWarning.IsLogWarningAsError ? LogLevel.Error : LogLevel.Warning,
            "[BadQueryWarning][IsLogWarningAsError:{IsLogWarningAsError}] Slow query execution. QueryElapsedTime.TotalMilliseconds:{QueryElapsedTime}. SlowQueryMillisecondsThreshold:{SlowQueryMillisecondsThreshold}. " +
            "BadQueryString:[{QueryString}]. " +
            "BadQueryStringTrackTrace:{TrackTrace}",
            persistenceConfiguration.BadQueryWarning.IsLogWarningAsError,
            queryElapsedTime.TotalMilliseconds,
            persistenceConfiguration.BadQueryWarning.SlowQueryMillisecondsThreshold,
            resultQueryStringBuilder?.Invoke(),
            loggingStackTrace);
    }

    public static void LogTooMuchDataInMemoryBadQueryWarning(
        int totalCount,
        ILogger logger,
        IPlatformPersistenceConfiguration persistenceConfiguration,
        string loggingStackTrace,
        [AllowNull] Func<string> resultQueryStringBuilder)
    {
        logger.Log(
            persistenceConfiguration.BadQueryWarning.IsLogWarningAsError ? LogLevel.Error : LogLevel.Warning,
            "[BadQueryWarning][IsLogWarningAsError:{IsLogWarningAsError}] Get too much of items into memory query execution. TotalItems:{TotalItems}; Threshold:{Threshold}. " +
            "BadQueryString:[{QueryString}]. " +
            "BadQueryStringTrackTrace:{TrackTrace}",
            persistenceConfiguration.BadQueryWarning.IsLogWarningAsError,
            totalCount,
            persistenceConfiguration.BadQueryWarning.TotalItemsThreshold,
            resultQueryStringBuilder?.Invoke(),
            loggingStackTrace);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default);

    public IQueryable<TEntity> GetQuery<TEntity>() where TEntity : class, IEntity;

    public void RunCommand(string command);

    public Task Initialize(IServiceProvider serviceProvider);

    public Task<TSource?> FirstOrDefaultAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public Task<TSource> FirstAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public Task<int> CountAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity;

    public Task<int> CountAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    public Task<bool> AnyAsync<TEntity>(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity;

    public Task<bool> AnyAsync<TSource>(
        IQueryable<TSource> source,
        CancellationToken cancellationToken = default);

    Task<TResult> FirstOrDefaultAsync<TEntity, TResult>(
        Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    Task<List<T>> GetAllAsync<T>(IQueryable<T> source, CancellationToken cancellationToken = default);

    Task<List<TResult>> GetAllAsync<TEntity, TResult>(
        Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity;

    public Task<List<TEntity>> CreateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public async Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var toUpdateEntities = await GetAllAsync<TEntity, TEntity>(query => query.Where(predicate), cancellationToken)
            .ThenAction(items => items.ForEach(updateAction));

        return await UpdateManyAsync<TEntity, TPrimaryKey>(toUpdateEntities, dismissSendEvent, sendEntityEventConfigure, cancellationToken);
    }

    public Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TPrimaryKey entityId,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public async Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var toDeleteEntities = await GetAllAsync(GetQuery<TEntity>().Where(predicate), cancellationToken);

        return await DeleteManyAsync<TEntity, TPrimaryKey>(toDeleteEntities, dismissSendEvent, sendEntityEventConfigure, cancellationToken);
    }

    public Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        Expression<Func<TEntity, bool>>? customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    /// <summary>
    /// CreateOrUpdateManyAsync. <br />
    /// Example for customCheckExistingPredicate: createOrUpdateEntity => existingEntity => existingEntity.XXX == createOrUpdateEntity.XXX
    /// </summary>
    public Task<List<TEntity>> CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        Func<TEntity, Expression<Func<TEntity, bool>>>? customCheckExistingPredicateBuilder = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent<TEntity>>? sendEntityEventConfigure = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public async Task EnsureEntitiesValid<TEntity, TPrimaryKey>(List<TEntity> entities, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await entities.EnsureEntitiesValid<TEntity, TPrimaryKey>(
            (predicate, token) => AnyAsync(GetQuery<TEntity>().Where(predicate), token),
            cancellationToken);
    }

    public async Task EnsureEntityValid<TEntity, TPrimaryKey>(TEntity entity, CancellationToken cancellationToken)
        where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        await entity.EnsureEntityValid<TEntity, TPrimaryKey>(
            (predicate, token) => AnyAsync(GetQuery<TEntity>().Where(predicate), token),
            cancellationToken);
    }
}

public interface IPlatformDbContext<TDbContext> : IPlatformDbContext where TDbContext : IPlatformDbContext<TDbContext>
{
    public Task MigrateApplicationDataAsync(IServiceProvider serviceProvider);
}
