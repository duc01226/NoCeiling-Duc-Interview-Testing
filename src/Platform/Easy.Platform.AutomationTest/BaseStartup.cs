using Easy.Platform.AutomationTest.Extensions.DependencyInjection;
using Easy.Platform.AutomationTest.TestCases;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest;

public abstract class BaseStartup
{
    public const string DefaultAutomationTestSettingsConfigurationSection = "AutomationTestSettings";

    public static readonly Lazy<IServiceProvider> GlobalLazyDiServiceProvider = new(valueFactory: () => GlobalDiServices!.BuildServiceProvider());
    public static IServiceProvider GlobalDiServiceProvider => GlobalLazyDiServiceProvider.Value;

    public static IServiceCollection GlobalDiServices { get; private set; } = new ServiceCollection();

    public IServiceCollection Services { get; private set; } = new ServiceCollection();

    /// <summary>
    /// Populate all startup services might created from host builder, return ServiceCollection contain all services generated from the start-up
    /// configuration for host builder.
    /// </summary>
    /// <typeparam name="TStartUp"></typeparam>
    /// <param name="startupFactory"></param>
    /// <returns></returns>
    public static IServiceCollection SpecFlowConfigureServices<TStartUp>(Func<TStartUp> startupFactory) where TStartUp : BaseStartup
    {
        var hostBuilder = new HostBuilder();
        var startUp = startupFactory();

        startUp.ConfigureHostConfiguration(hostBuilder);
        hostBuilder.ConfigureServices(configureDelegate: services => startUp.ConfigureServices(services));

        // Populate ServiceCollection to ExposeServiceCollectionFactory through UseServiceProviderFactory
        // and hostBuilder.Build() => trigger CreateBuilder => expose ServiceCollection
        var factory = new ExposeServiceCollectionFactory();
        hostBuilder.UseServiceProviderFactory(factory);
        hostBuilder.Build();

        return factory.ServiceCollection;
    }

    public virtual void ConfigureHost(IHostBuilder hostBuilder)
    {
        ConfigureHostConfiguration(hostBuilder);
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
        Services = services;
        GlobalDiServices = services;

        services.AddTransient(
            serviceType: typeof(IWebDriverManager),
            implementationFactory: sp => new WebDriverManager(settings: sp.GetRequiredService<AutomationTestSettings>())
                .With(p => p.ConfigWebDriverOptions = ConfigWebDriverOptions));

        services.AddTransient(serviceType: typeof(AutomationTestSettings), AutomationTestSettingsProvider);

        services.AddScoped<WebDriverLazyInitializer, WebDriverLazyInitializer>();
        services.AddSingleton<GlobalWebDriver, GlobalWebDriver>();
        services.RegisterAllFromType(conventionalType: typeof(IBddStepsContext), GetType().Assembly, ServiceLifeTime.Scoped);
    }

    /// <summary>
    /// Optional override to config WebDriverManager DriverOptions
    /// </summary>
    public virtual void ConfigWebDriverOptions(IOptions options) { }

    /// <summary>
    /// Default register AutomationTestSettings via IConfiguration first level binding using section key <see cref="DefaultAutomationTestSettingsConfigurationSection" />. Override this to custom
    /// </summary>
    public virtual AutomationTestSettings AutomationTestSettingsProvider(IServiceProvider sp)
    {
        return sp.GetRequiredService<IConfiguration>().GetSection(DefaultAutomationTestSettingsConfigurationSection).Get<AutomationTestSettings>()!;
    }

    public virtual void ConfigureHostConfiguration(IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureHostConfiguration(
            configureDelegate: builder => builder.AddConfiguration(config: PlatformConfigurationBuilder.GetConfigurationBuilder().Build()));
    }
}
