using Easy.Platform.Common.Utils;

namespace Easy.Platform.Infrastructures.BackgroundJob;

/// <summary>
/// Specify cronExpression for recurring job interval <br/>
/// Cron expression references: https://en.wikipedia.org/wiki/Cron
/// <br/>
/// Format: [minute(0-59)] [hour(0-23)] [day of month (1-31)] [month (1-12)] [dat of week (0-6 = Sunday to Saturday)]
/// <br/>
/// Example:
/// <br/>
/// Minutely: * * * * *
/// <br/>
/// Hourly: 0 * * * *
/// <br/>
/// Run once a year at midnight of 1 January: 0 0 1 1 *
/// <br/>
/// Run daily at midnight 0h: 0 0 * * *
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class PlatformRecurringJobAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformRecurringJobAttribute"/> class.
    /// Specify cronExpression for recurring job interval <br/>
    /// Cron expression references: https://en.wikipedia.org/wiki/Cron
    /// <br/>
    /// Format: [minute(0-59)] [hour(0-23)] [day of month (1-31)] [month (1-12)] [dat of week (0-6 = Sunday to Saturday)]
    /// <br/>
    /// Example:
    /// <br/>
    /// Minutely: * * * * *
    /// <br/>
    /// Hourly: 0 * * * *
    /// <br/>
    /// Run once a year at midnight of 1 January: 0 0 1 1 *
    /// <br/>
    /// Run daily at midnight 0h: 0 0 * * *
    /// </summary>
    /// <param name="cronExpression"><see cref="CronExpression"/></param>
    public PlatformRecurringJobAttribute(string cronExpression)
    {
        CronExpression = cronExpression;
    }

    /// <summary>
    /// Add or update a recurring job. Use <see cref="Util.CronBuilder"/> for common cron// </summary>
    /// Set the cronExpression to be used if TJobExecutor don't have <see cref="PlatformRecurringJobAttribute"/>
    /// https://en.wikipedia.org/wiki/Cron
    /// <br/>
    /// Format: [minute(0-59)] [hour(0-23)] [day of month (1-31)] [month (1-12)] [dat of week (0-6 = Sunday to Saturday)]
    /// <br/>
    /// Example:
    /// <br/>
    /// Minutely: * * * * *
    /// <br/>
    /// Hourly: 0 * * * *
    /// <br/>
    /// Run once a year at midnight of 1 January: 0 0 1 1 *
    /// <br/>
    /// Run daily at midnight 0h: 0 0 * * *
    public string CronExpression { get; }

    public static string GetCronExpressionInfo<TJobExecutor>() where TJobExecutor : IPlatformBackgroundJobExecutor
    {
        return GetCronExpressionInfo(typeof(TJobExecutor));
    }

    /// <summary>
    /// <inheritdoc cref="GetCronExpressionInfo{TJobExecutor}"/>
    /// </summary>
    public static string GetCronExpressionInfo(Type jobExecutorType)
    {
        var recurringJobAttribute = jobExecutorType
            .GetCustomAttributes(typeof(PlatformRecurringJobAttribute), true)
            .Select(p => (PlatformRecurringJobAttribute)p)
            .FirstOrDefault();
        return recurringJobAttribute?.CronExpression;
    }
}
