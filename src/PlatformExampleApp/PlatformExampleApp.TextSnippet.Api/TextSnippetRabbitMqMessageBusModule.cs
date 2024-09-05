using Easy.Platform.Application;
using Easy.Platform.Infrastructures.MessageBus;
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
            .With(m => m.ClientProvidedName = serviceProvider.GetService<IPlatformApplicationSettingContext>()!.ApplicationName);

        return options;
    }

    protected override PlatformMessageBusConfig MessageBusConfigFactory(IServiceProvider sp)
    {
        var options = Configuration.GetSection("RabbitMqOptions")
            .Get<PlatformMessageBusConfig>();

        return options;
    }
}
