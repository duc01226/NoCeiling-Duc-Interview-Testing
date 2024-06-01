using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.Infrastructures.BackgroundJob;

/// <summary>
/// Interface for a background job executor.
/// </summary>
public interface IPlatformBackgroundJobExecutor
{
    /// <summary>
    /// This method will be executed when processing the job
    /// </summary>
    public void Execute();

    /// <summary>
    /// Config the time in milliseconds to log warning if the process job time is over ProcessWarningTimeMilliseconds.
    /// </summary>
    public double? SlowProcessWarningTimeMilliseconds();
}

/// <summary>
/// Interface for a background job executor with param
/// </summary>
public interface IPlatformBackgroundJobExecutor<in TParam> : IPlatformBackgroundJobExecutor
{
    /// <summary>
    /// This method will be executed when processing the job
    /// </summary>
    public void Execute(TParam param);
}

/// <summary>
/// Base class for any background job executor with param. Define a job be extend from this class.
/// </summary>
public abstract class PlatformBackgroundJobExecutor<TParam> : IPlatformBackgroundJobExecutor<TParam> where TParam : class
{
    protected readonly ILogger Logger;

    public PlatformBackgroundJobExecutor(ILoggerFactory loggerFactory, IPlatformRootServiceProvider rootServiceProvider)
    {
        RootServiceProvider = rootServiceProvider;
        Logger = loggerFactory.CreateLogger(typeof(PlatformBackgroundJobExecutor).GetFullNameOrGenericTypeFullName() + $"-{GetType().Name}");
    }

    protected IPlatformRootServiceProvider RootServiceProvider { get; }

    /// <summary>
    /// Config the time in milliseconds to log warning if the process job time is over ProcessWarningTimeMilliseconds.
    /// </summary>
    public virtual double? SlowProcessWarningTimeMilliseconds()
    {
        return null;
    }

    public virtual void Execute(TParam param)
    {
        try
        {
            if (SlowProcessWarningTimeMilliseconds() > 0)
            {
                Logger.LogInformation("BackgroundJobExecutor invoking background job {GetTypeFullName} STARTED", GetType().FullName);

                Util.TaskRunner
                    .ProfileExecutionAsync(
                        asyncTask: () => InternalExecuteAsync(param),
                        afterExecution: elapsedMilliseconds =>
                        {
                            var logMessage =
                                $"ElapsedMilliseconds:{elapsedMilliseconds}.";

                            if (elapsedMilliseconds >= SlowProcessWarningTimeMilliseconds())
                                Logger.LogWarning(
                                    "BackgroundJobExecutor invoking background job {GetTypeFullName} FINISHED. SlowProcessWarningTimeMilliseconds:{SlowProcessWarningTimeMilliseconds()}. {LogMessage}",
                                    GetType().FullName,
                                    SlowProcessWarningTimeMilliseconds(),
                                    logMessage);
                            else
                                Logger.LogInformation(
                                    "BackgroundJobExecutor invoking background job {GetTypeFullName} FINISHED. {LogMessage}",
                                    GetType().FullName,
                                    logMessage);
                        })
                    .WaitResult();
            }
            else
            {
                Logger.LogInformation("BackgroundJobExecutor invoking background job {GetTypeFullName} STARTED", GetType().FullName);

                InternalExecuteAsync(param).WaitResult();

                Logger.LogInformation("BackgroundJobExecutor invoking background job {GetTypeFullName} FINISHED", GetType().FullName);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e.BeautifyStackTrace(), "[BackgroundJob] Job {BackgroundJobType_Name} execution was failed.", GetType().Name);
            throw;
        }
    }

    public virtual void Execute()
    {
        Execute(null);
    }

    public abstract Task ProcessAsync(TParam param);

    protected virtual async Task InternalExecuteAsync(TParam param)
    {
        await ProcessAsync(param);
    }
}

/// <summary>
/// Base class for any background job executor. Define a job be extend from this class.
/// </summary>
public abstract class PlatformBackgroundJobExecutor : PlatformBackgroundJobExecutor<object>
{
    protected PlatformBackgroundJobExecutor(ILoggerFactory loggerFactory, IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, rootServiceProvider)
    {
    }
}
