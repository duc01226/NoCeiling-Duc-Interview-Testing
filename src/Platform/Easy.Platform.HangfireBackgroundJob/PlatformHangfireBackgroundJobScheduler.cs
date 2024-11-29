#nullable enable
using System.Linq.Expressions;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Infrastructures.BackgroundJob;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.HangfireBackgroundJob;

public class PlatformHangfireBackgroundJobScheduler : IPlatformBackgroundJobScheduler
{
    public const string AutoRecurringJobIdByTypeSuffix = "_Auto_";
    public const int DefaultMaxLengthJobId = 100;
    private readonly IPlatformBackgroundJobSchedulerCarryRequestContextService? carryRequestContextService;

    private readonly IServiceProvider serviceProvider;

    public PlatformHangfireBackgroundJobScheduler(
        IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        carryRequestContextService = serviceProvider.GetService<IPlatformBackgroundJobSchedulerCarryRequestContextService>();
    }

    public string Schedule(Expression<Action> methodCall, TimeSpan? delay = null)
    {
        return BackgroundJob.Schedule(methodCall, delay ?? TimeSpan.Zero);
    }

    public string Schedule(Expression<Func<Task>> methodCall, TimeSpan? delay = null)
    {
        return BackgroundJob.Schedule(methodCall, delay ?? TimeSpan.Zero);
    }

    public string Schedule<TJobExecutor>(DateTimeOffset enqueueAt)
        where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        return BackgroundJob.Schedule(() => ExecuteBackgroundJobByType(typeof(TJobExecutor), null, CurrentRequestContextValuesAsJsonStr()), enqueueAt);
    }

    public string Schedule<TJobExecutor, TJobExecutorParam>(
        DateTimeOffset enqueueAt,
        TJobExecutorParam? jobExecutorParam)
        where TJobExecutor : IPlatformBackgroundJobExecutor<TJobExecutorParam>
        where TJobExecutorParam : class
    {
        return BackgroundJob.Schedule(
            () => ExecuteBackgroundJobByType(
                typeof(TJobExecutor),
                jobExecutorParam != null ? jobExecutorParam.ToJson(true) : null,
                CurrentRequestContextValuesAsJsonStr()),
            enqueueAt);
    }

    public string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt)
    {
        return BackgroundJob.Schedule(methodCall, enqueueAt);
    }

    public string Schedule<TJobExecutor>(TimeSpan? delay = null) where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        return BackgroundJob.Schedule(() => ExecuteBackgroundJobByType(typeof(TJobExecutor), null, CurrentRequestContextValuesAsJsonStr()), delay ?? TimeSpan.Zero);
    }

    public string Schedule<TJobExecutor, TJobExecutorParam>(
        TJobExecutorParam? jobExecutorParam,
        TimeSpan? delay = null)
        where TJobExecutor : IPlatformBackgroundJobExecutor<TJobExecutorParam> where TJobExecutorParam : class
    {
        return BackgroundJob.Schedule(
            () => ExecuteBackgroundJobByType(
                typeof(TJobExecutor),
                jobExecutorParam != null ? jobExecutorParam.ToJson(true) : null,
                CurrentRequestContextValuesAsJsonStr()),
            delay ?? TimeSpan.Zero);
    }

    public void UpsertRecurringJob<TJobExecutor>(Func<string>? cronExpression = null, TimeZoneInfo? timeZone = null)
        where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        var cronExpressionValue = EnsureValidToUpsertRecurringJob(typeof(TJobExecutor), cronExpression);

        RecurringJob.AddOrUpdate(
            BuildAutoRecurringJobIdByType<TJobExecutor>(),
            () => ExecuteBackgroundJobByType(typeof(TJobExecutor), null, CurrentRequestContextValuesAsJsonStr()),
            cronExpressionValue,
            new RecurringJobOptions
            {
                TimeZone = timeZone ?? TimeZoneInfo.Local
            });
    }

    public void UpsertRecurringJob<TJobExecutor, TJobExecutorParam>(
        TJobExecutorParam? jobExecutorParam,
        Func<string>? cronExpression = null,
        TimeZoneInfo? timeZone = null)
        where TJobExecutor : IPlatformBackgroundJobExecutor<TJobExecutorParam> where TJobExecutorParam : class
    {
        var cronExpressionValue = EnsureValidToUpsertRecurringJob(typeof(TJobExecutor), cronExpression);
        var jobExecutorParamJson = jobExecutorParam?.ToJson(true);

        RecurringJob.AddOrUpdate(
            BuildAutoRecurringJobIdByType<TJobExecutor>(),
            () => ExecuteBackgroundJobByType(typeof(TJobExecutor), jobExecutorParamJson, CurrentRequestContextValuesAsJsonStr()),
            cronExpressionValue,
            new RecurringJobOptions
            {
                TimeZone = timeZone ?? TimeZoneInfo.Local
            });
    }

    public void UpsertRecurringJob(
        Type jobExecutorType,
        Func<string>? cronExpression = null,
        TimeZoneInfo? timeZone = null)
    {
        var cronExpressionValue = EnsureValidToUpsertRecurringJob(jobExecutorType, cronExpression);

        RecurringJob.AddOrUpdate(
            BuildAutoRecurringJobIdByType(jobExecutorType),
            () => ExecuteBackgroundJobByType(jobExecutorType, null, CurrentRequestContextValuesAsJsonStr()),
            cronExpressionValue,
            new RecurringJobOptions
            {
                TimeZone = timeZone ?? TimeZoneInfo.Local
            });
    }

    public void UpsertRecurringJob(Type jobExecutorType, object? jobExecutorParam, Func<string>? cronExpression = null, TimeZoneInfo? timeZone = null)
    {
        var cronExpressionValue = EnsureValidToUpsertRecurringJob(jobExecutorType, cronExpression);
        var jobExecutorParamJson = jobExecutorParam?.ToJson(true);

        RecurringJob.AddOrUpdate(
            BuildAutoRecurringJobIdByType(jobExecutorType),
            () => ExecuteBackgroundJobByType(jobExecutorType, jobExecutorParamJson, CurrentRequestContextValuesAsJsonStr()),
            cronExpressionValue,
            new RecurringJobOptions { TimeZone = timeZone ?? TimeZoneInfo.Local });
    }

    public void UpsertRecurringJob(
        string recurringJobId,
        Type jobExecutorType,
        Func<string>? cronExpression = null,
        TimeZoneInfo? timeZone = null)
    {
        var cronExpressionValue = EnsureValidToUpsertRecurringJob(jobExecutorType, cronExpression);

        RecurringJob.AddOrUpdate(
            recurringJobId.TakeTop(DefaultMaxLengthJobId),
            () => ExecuteBackgroundJobByType(jobExecutorType, null, CurrentRequestContextValuesAsJsonStr()),
            cronExpressionValue,
            new RecurringJobOptions { TimeZone = timeZone ?? TimeZoneInfo.Local });
    }

    public void UpsertRecurringJob(
        string recurringJobId,
        Type jobExecutorType,
        object? jobExecutorParam,
        Func<string>? cronExpression = null,
        TimeZoneInfo? timeZone = null)
    {
        var cronExpressionValue = EnsureValidToUpsertRecurringJob(jobExecutorType, cronExpression);
        var jobExecutorParamJson = jobExecutorParam?.ToJson(true);

        RecurringJob.AddOrUpdate(
            recurringJobId.TakeTop(DefaultMaxLengthJobId),
            () => ExecuteBackgroundJobByType(jobExecutorType, jobExecutorParamJson, CurrentRequestContextValuesAsJsonStr()),
            cronExpressionValue,
            new RecurringJobOptions { TimeZone = timeZone ?? TimeZoneInfo.Local });
    }

    public void RemoveRecurringJobIfExist(string recurringJobId)
    {
        RecurringJob.RemoveIfExists(recurringJobId.TakeTop(DefaultMaxLengthJobId));
    }

    public void TriggerRecurringJob<TJobExecutor>() where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        RecurringJob.TriggerJob(BuildAutoRecurringJobIdByType<TJobExecutor>());
    }

    public void RemoveAllRecurringJobs()
    {
        var allExistingRecurringJobIds = AllExistingRecurringJobIds();

        foreach (var recurringJobId in allExistingRecurringJobIds)
            RecurringJob.RemoveIfExists(recurringJobId);
    }

    public HashSet<string> AllExistingRecurringJobIds()
    {
        using (var connection = JobStorage.Current.GetConnection())
        {
            return connection.GetRecurringJobs().Select(p => p.Id).ToHashSet();
        }
    }

    public void ReplaceAllRecurringBackgroundJobs(List<IPlatformBackgroundJobExecutor> newAllRecurringJobs)
    {
        var newCurrentRecurringJobExecutorToIdPairs = newAllRecurringJobs
            .Select(p => (JobExecutor: p, JobExecutorId: BuildAutoRecurringJobIdByType(p.GetType())))
            .ToHashSet();

        var newCurrentRecurringJobExecutorIds = newCurrentRecurringJobExecutorToIdPairs
            .Select(p => p.JobExecutorId)
            .ToHashSet();
        var allExistingRecurringJobIds = AllExistingRecurringJobIds();

        // Remove obsolete recurring job, job is not existed in the all current recurring declared jobs in source code
        foreach (var existingAutoRecurringJobId in allExistingRecurringJobIds)
        {
            if (!newCurrentRecurringJobExecutorIds.Contains(existingAutoRecurringJobId))
                RemoveRecurringJobIfExist(existingAutoRecurringJobId);
        }

        // Upsert all new recurring jobs
        newCurrentRecurringJobExecutorToIdPairs
            .Where(p => !allExistingRecurringJobIds.Contains(p.JobExecutorId))
            .Select(p => p.JobExecutor)
            .ParallelAsync(
                recurringBackgroundJobExecutor =>
                {
                    return Task.Run(
                        () =>
                        {
                            var backgroundJobTimeZoneOffset = PlatformRecurringJobAttribute.GetRecurringJobAttributeInfo(recurringBackgroundJobExecutor.GetType())
                                .TimeZoneOffset;

                            var backgroundJobTimeZoneInfo = backgroundJobTimeZoneOffset != null
                                ? TimeZoneInfo.GetSystemTimeZones().MinBy(p => Math.Abs(p.BaseUtcOffset.TotalHours - backgroundJobTimeZoneOffset.Value))
                                : null;

                            UpsertRecurringJob(recurringBackgroundJobExecutor.GetType(), timeZone: backgroundJobTimeZoneInfo);
                        });
                });
    }

    public void ExecuteBackgroundJob<TJobExecutor>() where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        ExecuteBackgroundJobByType(typeof(TJobExecutor), null, CurrentRequestContextValuesAsJsonStr());
    }

    public void ExecuteBackgroundJobWithParam<TJobExecutor>(object? jobExecutorParam)
        where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        ExecuteBackgroundJobByType(
            typeof(TJobExecutor),
            jobExecutorParam?.ToJson(true),
            CurrentRequestContextValuesAsJsonStr());
    }

    public string BuildAutoRecurringJobIdByType<TJobExecutor>() where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        return BuildAutoRecurringJobIdByType(typeof(TJobExecutor));
    }

    public string BuildAutoRecurringJobIdByType(Type jobExecutorType)
    {
        EnsureJobExecutorTypeValid(jobExecutorType);

        return $"{AutoRecurringJobIdByTypeSuffix}.{jobExecutorType.Name}".TakeTop(DefaultMaxLengthJobId);
    }

    public void ExecuteBackgroundJob<TJobExecutor>(TJobExecutor jobExecutor) where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        ExecuteBackgroundJobByInstance(jobExecutor, null);
    }

    public void ExecuteBackgroundJob<TJobExecutor, TParam>(TJobExecutor jobExecutor, TParam? jobExecutorParam) where TJobExecutor : IPlatformBackgroundJobExecutor<TParam>
    {
        ExecuteBackgroundJobByInstance(jobExecutor, jobExecutorParam?.ToJson(true));
    }

    public string Schedule<TJobExecutorParam>(Type jobExecutorType, TJobExecutorParam? jobExecutorParam, DateTimeOffset enqueueAt) where TJobExecutorParam : class
    {
        return BackgroundJob.Schedule(
            () => ExecuteBackgroundJobByType(
                jobExecutorType,
                jobExecutorParam != null ? jobExecutorParam.ToJson(true) : null,
                CurrentRequestContextValuesAsJsonStr()),
            enqueueAt);
    }

    public void ExecuteBackgroundJobByType(Type jobExecutorType, string? jobExecutorParamJson, string? requestContextJson)
    {
        EnsureJobExecutorTypeValid(jobExecutorType);

        using (var scope = serviceProvider.CreateScope())
        {
            var jobExecutor = scope.ServiceProvider.GetService(jobExecutorType);
            if (jobExecutor != null)
            {
                carryRequestContextService?.SetCurrentRequestContextValues(
                    scope,
                    requestContextJson?.JsonDeserialize<Dictionary<string, object?>>() ?? []);

                ExecuteBackgroundJobByInstance(jobExecutor.Cast<IPlatformBackgroundJobExecutor>(), jobExecutorParamJson);
            }
        }
    }

    public static string EnsureValidToUpsertRecurringJob(Type jobExecutorType, Func<string>? cronExpression)
    {
        EnsureJobExecutorTypeValid(jobExecutorType);

        var cronExpressionValue =
            (cronExpression?.Invoke() ?? PlatformRecurringJobAttribute.GetRecurringJobAttributeInfo(jobExecutorType)?.CronExpression) ??
            throw new Exception("Either recurring job must have cron expression from PlatformRecurringJobAttribute or cronExpression param must be not null");
        return cronExpressionValue;
    }

    public static void ExecuteBackgroundJobByInstance(IPlatformBackgroundJobExecutor jobExecutor, string? jobExecutorParamJson)
    {
        var withParamJobExecutorType = jobExecutor
            .GetType()
            .GetInterfaces()
            .FirstOrDefault(
                x => x.IsAssignableToGenericType(typeof(IPlatformBackgroundJobExecutor<>)));

        if (withParamJobExecutorType != null)
        {
            // Parse job executor param to correct type
            var jobExecutorParamType = withParamJobExecutorType.GetGenericArguments()[0];
            var jobExecutorParam = jobExecutorParamJson != null
                ? PlatformJsonSerializer.Deserialize(jobExecutorParamJson, jobExecutorParamType)
                : null;

            // Execute job executor method
            var executeMethod = jobExecutor.GetType()
                .GetMethod(
                    nameof(IPlatformBackgroundJobExecutor.Execute),
                    [jobExecutorParamType]);
            executeMethod!.Invoke(
                jobExecutor,
                [jobExecutorParam]);
        }
        else
        {
            jobExecutor.Execute();
        }
    }

    public static void EnsureJobExecutorTypeValid(Type jobExecutorType)
    {
        if (!jobExecutorType.IsAssignableTo(typeof(IPlatformBackgroundJobExecutor)))
        {
            throw new Exception(
                "JobExecutor type is invalid. Must be assignable to IPlatformBackgroundJobExecutor");
        }
    }

    private string CurrentRequestContextValuesAsJsonStr()
    {
        return (carryRequestContextService?.CurrentRequestContext() ?? new Dictionary<string, object?>()).ToJson();
    }
}
