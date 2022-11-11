using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.HangfireBackgroundJob;

/// <summary>
/// Using service provider activator to resolve object when activate background job.
/// Activate by serviceProvider first. If not success then use class Activator.
/// </summary>
public class PlatformHangfireActivator : JobActivator
{
    private readonly IServiceProvider serviceProvider;

    public PlatformHangfireActivator(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public override object ActivateJob(Type type)
    {
        return serviceProvider.GetService(type) ?? Activator.CreateInstance(type);
    }

    public override JobActivatorScope BeginScope(JobActivatorContext context)
    {
        return new PlatformHangfireJobActivatorScope(serviceProvider.CreateScope());
    }
}

public class PlatformHangfireJobActivatorScope : JobActivatorScope
{
    private readonly IServiceScope serviceScope;

    public PlatformHangfireJobActivatorScope(IServiceScope serviceScope)
    {
        this.serviceScope = serviceScope ?? throw new ArgumentNullException(nameof(serviceScope));
    }

    public override object Resolve(Type type)
    {
        return serviceScope.ServiceProvider.GetService(type) ?? Activator.CreateInstance(type);
    }

    public override void DisposeScope()
    {
        serviceScope.Dispose();
    }
}
