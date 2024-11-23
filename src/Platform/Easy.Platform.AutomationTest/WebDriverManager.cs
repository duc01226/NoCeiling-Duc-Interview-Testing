using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;
using WebDriverManager;
using WebDriverManager.DriverConfigs;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager.Helpers;

namespace Easy.Platform.AutomationTest;

public interface IWebDriverManager
{
    public IWebDriver CreateWebDriver();

    public IWebDriver CreateRemoteWebDriver();
    public IWebDriver CreateRemoteWebDriver(DriverOptions driverOptions);

    /// <summary>
    /// Create choosing default web driver (Currently is Chrome Driver)
    /// </summary>
    public IWebDriver CreateLocalMachineWebDriver(string version = "Latest", Architecture architecture = Architecture.Auto);

    public IWebDriver CreateLocalMachineWebDriver(IDriverConfig config, string version = "Latest", Architecture architecture = Architecture.Auto);
}

public class WebDriverManager : IWebDriverManager
{
    public WebDriverManager(AutomationTestSettings settings)
    {
        Settings = settings;
    }

    public AutomationTestSettings Settings { get; }

    public Action<IOptions>? ConfigWebDriverOptions { get; set; }

    public IWebDriver CreateWebDriver()
    {
        var initialWebDriver = Settings.UseRemoteWebDriver
            ? CreateRemoteWebDriver()
            : CreateLocalMachineWebDriver();

        return initialWebDriver
            .WithIf(when: Settings.PageLoadTimeoutSeconds > 0, p => p.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(Settings.PageLoadTimeoutSeconds!.Value))
            .WithIf(when: ConfigWebDriverOptions != null, p => ConfigWebDriverOptions!(obj: p.Manage()));
    }

    public IWebDriver CreateRemoteWebDriver()
    {
        return CreateRemoteWebDriver(driverOptions: BuildDriverOptions(Settings));
    }

    public IWebDriver CreateRemoteWebDriver(DriverOptions driverOptions)
    {
        return new RemoteWebDriver(
            remoteAddress: new Uri(uriString: Settings.RemoteWebDriverUrl!),
            capabilities: driverOptions.ToCapabilities(),
            commandTimeout: TimeSpan.FromSeconds(Settings.RemoteWebDriverCommandTimeoutSeconds)).Pipe(DefaultConfigDriver);
    }

    public IWebDriver CreateLocalMachineWebDriver(string version = "Latest", Architecture architecture = Architecture.Auto)
    {
        return CreateLocalMachineWebDriver(config: BuildDefaultDriverConfig(Settings), version, architecture);
    }

    public IWebDriver CreateLocalMachineWebDriver(IDriverConfig config, string version = "Latest", Architecture architecture = Architecture.Auto)
    {
        new DriverManager().SetUpDriver(config, version, architecture);
        return new ChromeDriver(options: BuildChromeDriverOptions(Settings)).Pipe(DefaultConfigDriver);
    }

    public static DriverOptions BuildDriverOptions(AutomationTestSettings settings)
    {
        // AddArgument("no-sandbox") to fix https://stackoverflow.com/questions/22322596/selenium-error-the-http-request-to-the-remote-webdriver-timed-out-after-60-sec
        return settings.WebDriverType
            .WhenValue(
                AutomationTestSettings.WebDriverTypes.Chrome,
                then: _ => BuildChromeDriverOptions(settings)
                    .With(o => o.AddArgument("no-sandbox"))
                    .As<DriverOptions>())
            .WhenValue(
                AutomationTestSettings.WebDriverTypes.Firefox,
                then: _ => new FirefoxOptions()
                    .With(o => o.AddArguments(settings.GetWebDriverConfigArgumentsList()))
                    .As<DriverOptions>())
            .WhenValue(
                AutomationTestSettings.WebDriverTypes.Edge,
                then: _ => new EdgeOptions()
                    .With(o => o.AddArgument("no-sandbox"))
                    .With(o => o.AddArguments(settings.GetWebDriverConfigArgumentsList()))
                    .As<DriverOptions>())
            .Execute();
    }

    private static ChromeOptions BuildChromeDriverOptions(AutomationTestSettings settings)
    {
        return new ChromeOptions()
            .With(o => o.AddArguments(settings.GetWebDriverConfigArgumentsList()));
    }

    public static IDriverConfig BuildDefaultDriverConfig(AutomationTestSettings settings)
    {
        return settings.WebDriverType
            .WhenValue(AutomationTestSettings.WebDriverTypes.Chrome, then: _ => new ChromeConfig().As<IDriverConfig>())
            .WhenValue(AutomationTestSettings.WebDriverTypes.Firefox, then: _ => new FirefoxConfig().As<IDriverConfig>())
            .WhenValue(AutomationTestSettings.WebDriverTypes.Edge, then: _ => new EdgeConfig().As<IDriverConfig>())
            .Execute();
    }

    public static WebDriverManager New(AutomationTestSettings settings)
    {
        return new WebDriverManager(settings);
    }

    public static TDriver DefaultConfigDriver<TDriver>(TDriver webDriver) where TDriver : IWebDriver
    {
        webDriver.Manage().Window.Maximize();

        return webDriver;
    }
}
