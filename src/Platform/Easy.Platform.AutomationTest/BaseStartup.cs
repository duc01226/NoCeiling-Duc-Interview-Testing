using Easy.Platform.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Easy.Platform.AutomationTest;

public abstract class BaseStartup
{
    public static readonly Lazy<IServiceProvider> GlobalLazyDiServiceProvider = new(() => GlobalDiServices.BuildServiceProvider());
    public static IServiceProvider GlobalDiServiceProvider => GlobalLazyDiServiceProvider.Value;

    public static IServiceCollection GlobalDiServices { get; private set; } = new ServiceCollection();

    public virtual void ConfigureHost(IHostBuilder hostBuilder)
    {
        ConfigureHostConfiguration(hostBuilder);
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
        GlobalDiServices = services;

        services.AddTransient(typeof(IWebDriverManager), sp => new WebDriverManager(sp.GetRequiredService<TestSettings>()));
        RegisterSettingsFromConfiguration<TestSettings>(services);
        services.AddScoped<WebDriverLazyInitializer, WebDriverLazyInitializer>();
        services.AddSingleton<GlobalWebDriver, GlobalWebDriver>();
    }

    public static void RegisterSettingsFromConfiguration<TSettings>(IServiceCollection services) where TSettings : TestSettings
    {
        services.AddTransient(typeof(TSettings), sp => sp.GetRequiredService<IConfiguration>().Get<TSettings>());
    }

    public virtual void ConfigureHostConfiguration(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureHostConfiguration(
            builder => builder.AddConfiguration(PlatformConfigurationBuilder.GetConfigurationBuilder().Build()));
    }
}
