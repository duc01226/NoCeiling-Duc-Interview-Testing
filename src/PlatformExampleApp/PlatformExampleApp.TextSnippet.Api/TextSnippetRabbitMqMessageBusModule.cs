using Easy.Platform.Application.Context;
using Easy.Platform.Common.Extensions;
using Easy.Platform.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PlatformExampleApp.TextSnippet.Api;

public class TextSnippetRabbitMqMessageBusModule : PlatformRabbitMqMessageBusModule
{
    public TextSnippetRabbitMqMessageBusModule(IServiceProvider serviceProvider, IConfiguration configuration) :
        base(serviceProvider, configuration)
    {
    }

    protected override PlatformRabbitMqOptions RabbitMqOptionsFactory(IServiceProvider serviceProvider)
    {
        var options = Configuration.GetSection("RabbitMqOptions")
            .Get<PlatformRabbitMqOptions>()
            .With(_ => _.ClientProvidedName = serviceProvider.GetService<IPlatformApplicationSettingContext>()!.ApplicationName);

        return options;
    }
}
