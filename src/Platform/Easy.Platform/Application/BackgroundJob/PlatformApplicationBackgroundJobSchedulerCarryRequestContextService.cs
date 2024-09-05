using Easy.Platform.Application.RequestContext;
using Easy.Platform.Infrastructures.BackgroundJob;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Application.BackgroundJob;

public class PlatformApplicationBackgroundJobSchedulerCarryRequestContextService : IPlatformBackgroundJobSchedulerCarryRequestContextService
{
    private readonly IPlatformApplicationRequestContextAccessor requestContextAccessor;

    public PlatformApplicationBackgroundJobSchedulerCarryRequestContextService(IPlatformApplicationRequestContextAccessor requestContextAccessor)
    {
        this.requestContextAccessor = requestContextAccessor;
    }

    public IDictionary<string, object> CurrentRequestContext()
    {
        return requestContextAccessor.Current;
    }

    public void SetCurrentRequestContextValues(IServiceScope serviceScope, IDictionary<string, object> requestContextValues)
    {
        requestContextAccessor.Current.SetValues(requestContextValues);
    }
}
