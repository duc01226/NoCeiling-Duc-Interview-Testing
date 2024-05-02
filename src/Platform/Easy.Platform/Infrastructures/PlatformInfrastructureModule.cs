using Easy.Platform.Common;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Infrastructures.Abstract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.Infrastructures;

public abstract class PlatformInfrastructureModule : PlatformModule
{
    public new const int DefaultExecuteInitPriority = PlatformModule.DefaultExecuteInitPriority + (ExecuteInitPriorityNextLevelDistance * 3);

    public const int DefaultDependentOnPersistenceInitExecuteInitPriority = PlatformModule.DefaultExecuteInitPriority + (ExecuteInitPriorityNextLevelDistance * 1);

    public PlatformInfrastructureModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformInfrastructureService>(GetServicesRegisterScanAssemblies());
    }

    protected override void RegisterHelpers(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformHelper>(typeof(PlatformInfrastructureModule).Assembly);
        serviceCollection.RegisterAllFromType<IPlatformHelper>(GetServicesRegisterScanAssemblies());
    }
}
