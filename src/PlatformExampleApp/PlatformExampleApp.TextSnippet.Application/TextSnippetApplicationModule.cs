using Easy.Platform.Application;
using Easy.Platform.Application.Context;
using Microsoft.Extensions.Configuration;
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

    protected override List<Func<IConfiguration, Type>> GetModuleTypeDependencies()
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
        return new PlatformApplicationSettingContext
        {
            ApplicationName = TextSnippetApplicationConstants.ApplicationName,
            ApplicationAssembly = Assembly
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
}
