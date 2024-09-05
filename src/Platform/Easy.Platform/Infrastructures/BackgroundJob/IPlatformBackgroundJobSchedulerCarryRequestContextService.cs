using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures.BackgroundJob;

public interface IPlatformBackgroundJobSchedulerCarryRequestContextService
{
    public IDictionary<string, object?> CurrentRequestContext();

    public void SetCurrentRequestContextValues(IServiceScope serviceScope, IDictionary<string, object?> requestContextValues);
}
