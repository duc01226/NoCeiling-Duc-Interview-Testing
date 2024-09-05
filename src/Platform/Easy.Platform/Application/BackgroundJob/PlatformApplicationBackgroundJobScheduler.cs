using System.Linq.Expressions;
using Easy.Platform.Infrastructures.BackgroundJob;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Application.BackgroundJob;

public interface IPlatformApplicationBackgroundJobScheduler : IPlatformBackgroundJobScheduler
{
}

public class PlatformApplicationBackgroundJobScheduler : IPlatformApplicationBackgroundJobScheduler
{
    private readonly Lazy<IPlatformBackgroundJobScheduler> innerInfrastructureSchedulerLazy;

    public PlatformApplicationBackgroundJobScheduler(
        IServiceProvider serviceProvider)
    {
        innerInfrastructureSchedulerLazy = new Lazy<IPlatformBackgroundJobScheduler>(serviceProvider.GetRequiredService<IPlatformBackgroundJobScheduler>);
    }

    private IPlatformBackgroundJobScheduler InnerInfrastructureScheduler => innerInfrastructureSchedulerLazy.Value;

    public string Schedule(Expression<Action> methodCall, TimeSpan? delay = null)
    {
        return InnerInfrastructureScheduler.Schedule(methodCall, delay);
    }

    public string Schedule<TJobExecutor>(DateTimeOffset enqueueAt) where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        return InnerInfrastructureScheduler.Schedule<TJobExecutor>(enqueueAt);
    }

    public string Schedule<TJobExecutorParam>(Type jobExecutorType, TJobExecutorParam jobExecutorParam, DateTimeOffset enqueueAt) where TJobExecutorParam : class
    {
        return InnerInfrastructureScheduler.Schedule(jobExecutorType, jobExecutorParam, enqueueAt);
    }

    public string Schedule<TJobExecutor, TJobExecutorParam>(DateTimeOffset enqueueAt, TJobExecutorParam jobExecutorParam)
        where TJobExecutor : IPlatformBackgroundJobExecutor<TJobExecutorParam> where TJobExecutorParam : class
    {
        return InnerInfrastructureScheduler.Schedule<TJobExecutor, TJobExecutorParam>(enqueueAt, jobExecutorParam);
    }

    public string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt)
    {
        return InnerInfrastructureScheduler.Schedule(methodCall, enqueueAt);
    }

    public string Schedule<TJobExecutor>(TimeSpan? delay = null) where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        return InnerInfrastructureScheduler.Schedule<TJobExecutor>(delay);
    }

    public string Schedule<TJobExecutor, TJobExecutorParam>(TJobExecutorParam jobExecutorParam, TimeSpan? delay = null)
        where TJobExecutor : IPlatformBackgroundJobExecutor<TJobExecutorParam> where TJobExecutorParam : class
    {
        return InnerInfrastructureScheduler.Schedule<TJobExecutor, TJobExecutorParam>(jobExecutorParam, delay);
    }

    public void UpsertRecurringJob<TJobExecutor>(Func<string> cronExpression = null, TimeZoneInfo timeZone = null) where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        InnerInfrastructureScheduler.UpsertRecurringJob<TJobExecutor>(cronExpression, timeZone);
    }

    public void UpsertRecurringJob<TJobExecutor, TJobExecutorParam>(TJobExecutorParam jobExecutorParam, Func<string> cronExpression = null, TimeZoneInfo timeZone = null)
        where TJobExecutor : IPlatformBackgroundJobExecutor<TJobExecutorParam> where TJobExecutorParam : class
    {
        InnerInfrastructureScheduler.UpsertRecurringJob<TJobExecutor, TJobExecutorParam>(jobExecutorParam, cronExpression, timeZone);
    }

    public void UpsertRecurringJob(Type jobExecutorType, Func<string> cronExpression = null, TimeZoneInfo timeZone = null)
    {
        InnerInfrastructureScheduler.UpsertRecurringJob(jobExecutorType, cronExpression, timeZone);
    }

    public void UpsertRecurringJob(Type jobExecutorType, object jobExecutorParam, Func<string> cronExpression = null, TimeZoneInfo timeZone = null)
    {
        InnerInfrastructureScheduler.UpsertRecurringJob(jobExecutorType, jobExecutorParam, cronExpression, timeZone);
    }

    public void UpsertRecurringJob(string recurringJobId, Type jobExecutorType, Func<string> cronExpression = null, TimeZoneInfo timeZone = null)
    {
        InnerInfrastructureScheduler.UpsertRecurringJob(recurringJobId, jobExecutorType, cronExpression, timeZone);
    }

    public void UpsertRecurringJob(string recurringJobId, Type jobExecutorType, object jobExecutorParam, Func<string> cronExpression = null, TimeZoneInfo timeZone = null)
    {
        InnerInfrastructureScheduler.UpsertRecurringJob(recurringJobId, jobExecutorType, jobExecutorParam, cronExpression, timeZone);
    }

    public void RemoveRecurringJobIfExist(string recurringJobId)
    {
        InnerInfrastructureScheduler.RemoveRecurringJobIfExist(recurringJobId);
    }

    public void TriggerRecurringJob<TJobExecutor>() where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        InnerInfrastructureScheduler.TriggerRecurringJob<TJobExecutor>();
    }

    public void RemoveAllRecurringJobs()
    {
        InnerInfrastructureScheduler.RemoveAllRecurringJobs();
    }

    public HashSet<string> AllExistingRecurringJobIds()
    {
        return InnerInfrastructureScheduler.AllExistingRecurringJobIds();
    }

    public string BuildAutoRecurringJobIdByType<TJobExecutor>() where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        return InnerInfrastructureScheduler.BuildAutoRecurringJobIdByType<TJobExecutor>();
    }

    public string BuildAutoRecurringJobIdByType(Type jobExecutorType)
    {
        return InnerInfrastructureScheduler.BuildAutoRecurringJobIdByType(jobExecutorType);
    }

    public void ReplaceAllRecurringBackgroundJobs(List<IPlatformBackgroundJobExecutor> newAllRecurringJobs)
    {
        InnerInfrastructureScheduler.ReplaceAllRecurringBackgroundJobs(newAllRecurringJobs);
    }

    public void ExecuteBackgroundJob<TJobExecutor>() where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        InnerInfrastructureScheduler.ExecuteBackgroundJob<TJobExecutor>();
    }

    public void ExecuteBackgroundJobWithParam<TJobExecutor>(object jobExecutorParam) where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        InnerInfrastructureScheduler.ExecuteBackgroundJobWithParam<TJobExecutor>(jobExecutorParam);
    }

    public void ExecuteBackgroundJob<TJobExecutor>(TJobExecutor jobExecutor) where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        InnerInfrastructureScheduler.ExecuteBackgroundJob(jobExecutor);
    }

    public void ExecuteBackgroundJob<TJobExecutor, TParam>(TJobExecutor jobExecutor, TParam jobExecutorParam) where TJobExecutor : IPlatformBackgroundJobExecutor<TParam>
    {
        InnerInfrastructureScheduler.ExecuteBackgroundJob(jobExecutor, jobExecutorParam);
    }

    public void ExecuteBackgroundJobByType(Type jobExecutorType, string jobExecutorParamJson, string? requestContextJson)
    {
        InnerInfrastructureScheduler.ExecuteBackgroundJobByType(jobExecutorType, jobExecutorParamJson, requestContextJson);
    }
}
