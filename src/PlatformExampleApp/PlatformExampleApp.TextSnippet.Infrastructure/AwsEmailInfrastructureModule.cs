using Easy.Platform.Infrastructures;
using Microsoft.Extensions.Configuration;

namespace PlatformExampleApp.TextSnippet.Infrastructure;

/// <summary>
/// Example of implementation in other project assembly module. This folder should be in a separated project
/// </summary>
public class AwsEmailInfrastructureModule : PlatformInfrastructureModule
{
    public AwsEmailInfrastructureModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }
}
