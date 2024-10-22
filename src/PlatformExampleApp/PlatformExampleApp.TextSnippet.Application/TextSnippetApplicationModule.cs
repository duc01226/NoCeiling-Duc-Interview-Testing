using Easy.Platform.Application;
using Easy.Platform.Application.MessageBus.InboxPattern;
using Easy.Platform.Application.MessageBus.OutboxPattern;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformExampleApp.TextSnippet.Domain;

namespace PlatformExampleApp.TextSnippet.Application;

public class TextSnippetApplicationModule : PlatformApplicationModule
{
    public TextSnippetApplicationModule(
        IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    /// <summary>
    /// Override this to true to auto register default caching module, which include default memory caching repository.
    /// <br></br>
    /// Don't need to auto register if you have register a caching module manually
    /// </summary>
    protected override bool AutoRegisterDefaultCaching => false;

    public override List<Func<IConfiguration, Type>> ModuleTypeDependencies()
    {
        var result = new List<Func<IConfiguration, Type>>
        {
            p => typeof(TextSnippetDomainModule)
        };
        return result;
    }

    // Your application can either override factory method DefaultApplicationSettingContextFactory to register default PlatformApplicationSettingContext
    // or just declare a class implement IPlatformApplicationSettingContext in project to use. It will be automatically registered.
    // Example that the class TextSnippetApplicationSettingContext has replace the default application setting
    protected override PlatformApplicationSettingContext DefaultApplicationSettingContextFactory(
        IServiceProvider serviceProvider)
    {
        return new PlatformApplicationSettingContext(serviceProvider)
        {
            ApplicationName = TextSnippetApplicationConstants.ApplicationName,
            ApplicationAssembly = Assembly,
            IsDebugInformationMode = serviceProvider.GetRequiredService<IConfiguration>()
                .GetValue<bool?>(PlatformApplicationSettingContext.DefaultIsDebugInformationModeConfigurationKey) == true
        };
    }

    // Example Override this to set the whole application default JsonSerializerOptions for PlatformJsonSerializer.CurrentOptions
    // The platform use PlatformJsonSerializer.CurrentOptions for every Json Serialization Tasks
    //protected override JsonSerializerOptions JsonSerializerCurrentOptions()
    //{
    //    return PlatformJsonSerializer.BuildDefaultOptions(
    //        useCamelCaseNaming: false,
    //        useJsonStringEnumConverter: false,
    //        customConverters: new List<JsonConverter>()
    //        {
    //            /* Your custom converters if existed*/
    //        });
    //}

    // Demo override to config inbox/outbox config
    protected override PlatformInboxConfig InboxConfigProvider(IServiceProvider serviceProvider)
    {
        return base.InboxConfigProvider(serviceProvider).With(c => c.MaxStoreProcessedMessageCount = 100);
    }

    // Demo override to config inbox/outbox config
    protected override PlatformOutboxConfig OutboxConfigProvider(IServiceProvider serviceProvider)
    {
        return base.OutboxConfigProvider(serviceProvider).With(c => c.MaxStoreProcessedMessageCount = 100);
    }
}
