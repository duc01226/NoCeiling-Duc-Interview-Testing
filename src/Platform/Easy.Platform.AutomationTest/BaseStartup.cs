using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest;

public abstract class BaseStartup
{
    public static readonly Lazy<IServiceProvider> GlobalLazyDiServiceProvider = new(() => GlobalDiServices!.BuildServiceProvider());
    public static IServiceProvider GlobalDiServiceProvider => GlobalLazyDiServiceProvider.Value;

    public static IServiceCollection GlobalDiServices { get; private set; } = new ServiceCollection();

    public virtual void ConfigureHost(IHostBuilder hostBuilder)
    {
        ConfigureHostConfiguration(hostBuilder);
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
        GlobalDiServices = services;

        services.AddTransient(
            typeof(IWebDriverManager),
            sp => new WebDriverManager(sp.GetRequiredService<AutomationTestSettings>())
                .With(p => p.ConfigWebDriverOptions = ConfigWebDriverOptions));

        services.AddTransient(typeof(AutomationTestSettings), AutomationTestSettingsProvider);
        services.RegisterAllFromType<AutomationTestSettings>(GetType().Assembly, replaceIfExist: false);

        services.AddScoped<WebDriverLazyInitializer, WebDriverLazyInitializer>();
        services.AddSingleton<GlobalWebDriver, GlobalWebDriver>();
    }

    /// <summary>
    /// Optional override to config WebDriverManager DriverOptions
    /// </summary>
    public virtual void ConfigWebDriverOptions(IOptions options) { }

    /// <summary>
    /// Default register AutomationTestSettings via IConfiguration first level binding. Override this to custom
    /// </summary>
    public virtual AutomationTestSettings AutomationTestSettingsProvider(IServiceProvider sp)
    {
        return sp.GetRequiredService<IConfiguration>().Get<AutomationTestSettings>()!;
    }

    public virtual void ConfigureHostConfiguration(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureHostConfiguration(
            builder => builder.AddConfiguration(PlatformConfigurationBuilder.GetConfigurationBuilder().Build()));
    }
}
