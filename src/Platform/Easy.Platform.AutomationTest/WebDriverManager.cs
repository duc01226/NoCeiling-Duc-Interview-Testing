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
    public WebDriverManager(TestSettings settings)
    {
        Settings = settings;
    }

    protected TestSettings Settings { get; }

    public IWebDriver CreateWebDriver()
    {
        return Settings.UseRemoteWebDriver
            ? CreateRemoteWebDriver()
            : CreateLocalMachineWebDriver();
    }

    public IWebDriver CreateRemoteWebDriver()
    {
        // AddArgument("no-sandbox") to fix https://stackoverflow.com/questions/22322596/selenium-error-the-http-request-to-the-remote-webdriver-timed-out-after-60-sec
        return CreateRemoteWebDriver(BuildDefaultDriverOptions(Settings));
    }

    public IWebDriver CreateRemoteWebDriver(DriverOptions driverOptions)
    {
        return new RemoteWebDriver(new Uri(Settings.RemoteWebDriverUrl!), driverOptions).Pipe(DefaultConfigDriver);
    }

    public IWebDriver CreateLocalMachineWebDriver(string version = "Latest", Architecture architecture = Architecture.Auto)
    {
        return CreateLocalMachineWebDriver(BuildDefaultDriverConfig(Settings), version, architecture);
    }

    public IWebDriver CreateLocalMachineWebDriver(IDriverConfig config, string version = "Latest", Architecture architecture = Architecture.Auto)
    {
        new DriverManager().SetUpDriver(config, version, architecture);
        return new ChromeDriver().Pipe(DefaultConfigDriver);
    }

    public static DriverOptions BuildDefaultDriverOptions(TestSettings settings)
    {
        return settings.WebDriverType
            .WhenValue(TestSettings.WebDriverTypes.Chrome, _ => new ChromeOptions().Pipe(_ => _.AddArgument("no-sandbox")).As<DriverOptions>())
            .WhenValue(TestSettings.WebDriverTypes.Firefox, _ => new FirefoxOptions().As<DriverOptions>())
            .WhenValue(TestSettings.WebDriverTypes.Edge, _ => new EdgeOptions().As<DriverOptions>())
            .Execute();
    }

    public static IDriverConfig BuildDefaultDriverConfig(TestSettings settings)
    {
        return settings.WebDriverType
            .WhenValue(TestSettings.WebDriverTypes.Chrome, _ => new ChromeConfig().As<IDriverConfig>())
            .WhenValue(TestSettings.WebDriverTypes.Firefox, _ => new FirefoxConfig().As<IDriverConfig>())
            .WhenValue(TestSettings.WebDriverTypes.Edge, _ => new EdgeConfig().As<IDriverConfig>())
            .Execute();
    }

    public static WebDriverManager New(TestSettings settings)
    {
        return new WebDriverManager(settings);
    }

    public TDriver DefaultConfigDriver<TDriver>(TDriver webDriver) where TDriver : IWebDriver
    {
        webDriver.Manage().Window.Maximize();

        return webDriver;
    }
}
