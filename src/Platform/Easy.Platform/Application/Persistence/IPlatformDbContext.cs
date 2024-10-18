#nullable enable
using System.Diagnostics;
using System.Linq.Expressions;
using Easy.Platform.Common;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Timing;
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
    public static readonly int DefaultPageSize = Util.TaskRunner.DefaultParallelIoTaskMaxConcurrent;
    public static readonly int SaveMigrationHistoryRetryCount = 100;

    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformDbContext)}");
    public static int MigrationRetryCount => PlatformEnvironment.IsDevelopment ? 5 : 10;
    public static int MigrationRetryDelaySeconds => PlatformEnvironment.IsDevelopment ? 15 : 30;

    public IQueryable<PlatformDataMigrationHistory> ApplicationDataMigrationHistoryQuery { get; }

    public IPlatformUnitOfWork? MappedUnitOfWork { get; set; }

    public ILogger Logger { get; }

    public Task UpsertOneDataMigrationHistoryAsync(PlatformDataMigrationHistory entity, CancellationToken cancellationToken = default);

    public IQueryable<PlatformDataMigrationHistory> DataMigrationHistoryQuery();

    public Task ExecuteWithNewDbContextInstanceAsync(Func<IPlatformDbContext, Task> fn);

    public async Task MigrateApplicationDataAsync<TDbContext>(
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) where TDbContext : class, IPlatformDbContext<TDbContext>
    {
        PlatformDataMigrationExecutor<TDbContext>
            .EnsureAllDataMigrationExecutorsHasUniqueName(GetType().Assembly, serviceProvider);

        var canExecuteMigrations = PlatformDataMigrationExecutor<TDbContext>
            .GetCanExecuteDataMigrationExecutors(GetType().Assembly, serviceProvider, ApplicationDataMigrationHistoryQuery);

        var mainThreadMigrations = canExecuteMigrations.Where(p => !p.AllowRunInBackgroundThread).ToList();
        var backgroundThreadMigrations = canExecuteMigrations.Where(p => p.AllowRunInBackgroundThread).ToList();

        await mainThreadMigrations.ForEachAsync(async migrationExecution => await ExecuteDataMigrationExecutor(migrationExecution));
        Util.TaskRunner.QueueActionInBackground(
            async () =>
            {
                await backgroundThreadMigrations.ForEachAsync(
                    async migrationExecution =>
                    {
                        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                            () => rootServiceProvider.ExecuteInjectScopedAsync(
                                async (IServiceProvider sp, TDbContext dbContext) =>
                                    await dbContext.ExecuteDataMigrationExecutor(
                                        PlatformDataMigrationExecutor<TDbContext>.CreateNewInstance(sp, migrationExecution.GetType()))),
                            sleepDurationProvider: retry => MigrationRetryDelaySeconds.Seconds(),
                            retryCount: MigrationRetryCount);
                    });
            },
            loggerFactory: () => rootServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType()));
    }

    public static void LogDataMigrationFailedError(ILogger logger, Exception ex, string migrationExecutionName)
    {
        logger.LogError(
            ex.BeautifyStackTrace(),
            "DataMigration {DataMigrationName} FAILED. [Error:{Error}].",
            migrationExecutionName,
            ex.Message);
    }

    public async Task ExecuteDataMigrationExecutor<TDbContext>(PlatformDataMigrationExecutor<TDbContext> migrationExecution)
        where TDbContext : class, IPlatformDbContext
    {
        try
        {
            using (var activity = IPlatformCqrsEventHandler.ActivitySource.StartActivity())
            {
                activity?.AddTag("MigrationName", migrationExecution.Name);

                var existingMigrationHistory = DataMigrationHistoryQuery().FirstOrDefault(p => p.Name == migrationExecution.Name);

                if (existingMigrationHistory == null || existingMigrationHistory.CanStartOrRetryProcess())
                {
                    Logger.LogInformation("DataMigration {MigrationExecutionName} STARTED.", migrationExecution.Name);

                    var toUpsertMigrationHistory = existingMigrationHistory ?? new PlatformDataMigrationHistory(migrationExecution.Name);

                    await UpsertOneDataMigrationHistorySaveChangesImmediatelyAsync(
                        toUpsertMigrationHistory
                            .With(p => p.Status = PlatformDataMigrationHistory.Statuses.Processing)
                            .With(p => p.LastProcessError = null)
                            .With(p => p.LastProcessingPingTime = Clock.UtcNow));

                    var startIntervalPingProcessingMigrationHistoryCts = new CancellationTokenSource();

                    StartIntervalPingProcessingMigrationHistory(migrationExecution.Name, startIntervalPingProcessingMigrationHistoryCts.Token);

                    try
                    {
                        await migrationExecution.Execute(this.As<TDbContext>());

                        await startIntervalPingProcessingMigrationHistoryCts.CancelAsync();

                        // Delay to ensure interval finished
                        await Task.Delay(10.Seconds(), CancellationToken.None);

                        // Retry in case interval ping make it failed for concurrency token
                        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                            async () =>
                            {
                                var processingMigrationHistoryItem = DataMigrationHistoryQuery()
                                    .FirstOrDefault(p => p.Name == migrationExecution.Name && p.Status == PlatformDataMigrationHistory.Statuses.Processing);

                                if (processingMigrationHistoryItem != null)
                                    await UpsertOneDataMigrationHistoryAsync(
                                        processingMigrationHistoryItem
                                            .With(p => p.Status = PlatformDataMigrationHistory.Statuses.Processed)
                                            .With(p => p.LastProcessingPingTime = Clock.UtcNow),
                                        CancellationToken.None);

                                await SaveChangesAsync(CancellationToken.None);
                            },
                            retryTime => retryTime.Seconds(),
                            SaveMigrationHistoryRetryCount,
                            onRetry: (ex, delayRetryTime, retryAttempt, context) =>
                            {
                                if (retryAttempt > 3) Logger.LogError(ex.BeautifyStackTrace(), "Upsert DataMigrationHistory Status=Processed failed");
                            },
                            cancellationToken: CancellationToken.None);

                        this.As<TDbContext>().Logger.LogInformation("DataMigration {MigrationExecutionName} FINISHED.", migrationExecution.Name);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e.BeautifyStackTrace(), "DataMigrationHistory execution failed");

                        // Retry in case interval ping make it failed for concurrency token
                        await Util.TaskRunner.WaitRetryAsync(
                            async ct => await ExecuteWithNewDbContextInstanceAsync(
                                async newContextInstance =>
                                {
                                    var toUpdatePingTimeMigrationHistory = newContextInstance.DataMigrationHistoryQuery()
                                        .FirstOrDefault(p => p.Name == migrationExecution.Name && p.Status == PlatformDataMigrationHistory.Statuses.Processing);

                                    if (toUpdatePingTimeMigrationHistory != null)
                                        await newContextInstance.UpsertOneDataMigrationHistorySaveChangesImmediatelyAsync(
                                            toUpdatePingTimeMigrationHistory
                                                .With(p => p.Status = PlatformDataMigrationHistory.Statuses.Failed)
                                                .With(p => p.LastProcessError = e.Serialize()),
                                            CancellationToken.None);
                                }),
                            retryTime => retryTime.Seconds(),
                            SaveMigrationHistoryRetryCount,
                            (ex, delayRetryTime, retryAttempt, context) =>
                            {
                                if (retryAttempt > 3) Logger.LogError(ex.BeautifyStackTrace(), "Upsert DataMigrationHistory Status=Failed failed");
                            },
                            CancellationToken.None);
                        throw;
                    }
                    finally
                    {
                        if (!startIntervalPingProcessingMigrationHistoryCts.IsCancellationRequested)
                            await startIntervalPingProcessingMigrationHistoryCts.CancelAsync();
                        startIntervalPingProcessingMigrationHistoryCts.Dispose();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogDataMigrationFailedError(Logger, ex, migrationExecution.Name);

            throw;
        }
        finally
        {
            migrationExecution.Dispose();
            await Util.GarbageCollector.Collect(0);
        }
    }

    public void StartIntervalPingProcessingMigrationHistory(string migrationExecutionName, CancellationToken cancellationToken)
    {
        Util.TaskRunner.QueueActionInBackground(
            async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                    await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                        async () =>
                        {
                            try
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                    await ExecuteWithNewDbContextInstanceAsync(
                                        async newContextInstance =>
                                        {
                                            var toUpdatePingTimeMigrationHistory = newContextInstance.DataMigrationHistoryQuery()
                                                .FirstOrDefault(p => p.Name == migrationExecutionName && p.Status == PlatformDataMigrationHistory.Statuses.Processing);

                                            if (toUpdatePingTimeMigrationHistory != null)
                                                await newContextInstance.UpsertOneDataMigrationHistorySaveChangesImmediatelyAsync(
                                                    toUpdatePingTimeMigrationHistory.With(p => p.LastProcessingPingTime = Clock.UtcNow),
                                                    cancellationToken);
                                        });

                                await Task.Delay(PlatformDataMigrationHistory.ProcessingPingIntervalSeconds.Seconds(), CancellationToken.None);
                            }
                            catch (TaskCanceledException)
                            {
                                // Empty and skip taskCanceledException
                            }
                        },
                        cancellationToken: CancellationToken.None,
                        retryCount: SaveMigrationHistoryRetryCount,
                        onRetry: (ex, delayRetryTime, retryAttempt, context) =>
                        {
                            if (retryAttempt > 3) Logger.LogError(ex.BeautifyStackTrace(), "Upsert DataMigrationHistory LastProcessingPingTime failed");
                        });
            },
            loggerFactory: () => Logger,
            cancellationToken: CancellationToken.None);
    }

    public async Task UpsertOneDataMigrationHistorySaveChangesImmediatelyAsync(
        PlatformDataMigrationHistory toUpsertMigrationHistory,
        CancellationToken cancellationToken = default)
    {
        await UpsertOneDataMigrationHistoryAsync(toUpsertMigrationHistory, cancellationToken);

        await SaveChangesAsync(cancellationToken);
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
        var loggingFullStackTrace = PlatformEnvironment.StackTrace();

        if (persistenceConfiguration.BadQueryWarning.TotalItemsThresholdWarningEnabled &&
            resultQuery != null &&
            typeof(TResult).IsAssignableToGenericType(typeof(IEnumerable<>)))
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
            IEnumerable<TSource>? resultQuery,
            IPlatformPersistenceConfiguration persistenceConfiguration,
            ILogger logger,
            string loggingFullStackTrace,
            Func<string>? resultQueryStringBuilder)
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
            Func<string>? resultQueryStringBuilder)
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
        Func<string>? resultQueryStringBuilder)
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
        Func<string>? resultQueryStringBuilder)
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
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> UpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> SetAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public async Task<List<TEntity>> UpdateManyAsync<TEntity, TPrimaryKey>(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new()
    {
        var toUpdateEntities = await GetAllAsync<TEntity, TEntity>(query => query.Where(predicate), cancellationToken)
            .ThenAction(items => items.ForEach(updateAction));

        return await UpdateManyAsync<TEntity, TPrimaryKey>(toUpdateEntities, dismissSendEvent, eventCustomConfig, cancellationToken);
    }

    public Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TPrimaryKey entityId,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> DeleteAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<List<TPrimaryKey>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<List<TEntity>> DeleteManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<int> DeleteManyAsync<TEntity, TPrimaryKey>(
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<int> DeleteManyAsync<TEntity, TPrimaryKey>(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> CreateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        bool dismissSendEvent,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        Expression<Func<TEntity, bool>>? customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    public Task<TEntity> CreateOrUpdateAsync<TEntity, TPrimaryKey>(
        TEntity entity,
        TEntity? existingEntity,
        Expression<Func<TEntity, bool>>? customCheckExistingPredicate = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
        CancellationToken cancellationToken = default) where TEntity : class, IEntity<TPrimaryKey>, new();

    /// <summary>
    /// CreateOrUpdateManyAsync. <br />
    /// Example for customCheckExistingPredicate: createOrUpdateEntity => existingEntity => existingEntity.XXX == createOrUpdateEntity.XXX
    /// </summary>
    public Task<List<TEntity>> CreateOrUpdateManyAsync<TEntity, TPrimaryKey>(
        List<TEntity> entities,
        Func<TEntity, Expression<Func<TEntity, bool>>>? customCheckExistingPredicateBuilder = null,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent>? eventCustomConfig = null,
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
