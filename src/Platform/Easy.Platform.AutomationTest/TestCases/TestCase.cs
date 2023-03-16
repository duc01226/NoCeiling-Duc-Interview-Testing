using System.Reflection;
using Easy.Platform.AutomationTest.Extensions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Easy.Platform.AutomationTest.TestCases;

public abstract class TestCase<TSettings> where TSettings : AutomationTestSettings
{
    private WebDriverWait? globalWebDriverWait;
    private WebDriverWait? webDriverWait;

    protected TestCase(IWebDriverManager driverManager, TSettings settings, IScopedLazyWebDriver lazyWebDriver, ISingletonLazyWebDriver globalLazyWebDriver)
    {
        DriverManager = driverManager;
        Settings = settings;
        LazyWebDriver = lazyWebDriver;
        GlobalLazyWebDriver = globalLazyWebDriver;
    }

    protected IWebDriverManager DriverManager { get; set; }
    protected TSettings Settings { get; set; }
    protected ILazyWebDriver LazyWebDriver { get; set; }
    protected ILazyWebDriver GlobalLazyWebDriver { get; }

    protected IWebDriver WebDriver => LazyWebDriver.Value;

    /// <summary>
    /// Use GlobalWebDriver all test cases will run in the same web browser instance. <br />
    /// Use it if you want to keep session, web browser data state, local storage, etc .. <br />
    /// between test cases
    /// </summary>
    protected IWebDriver GlobalWebDriver => GlobalLazyWebDriver.Value;

    protected WebDriverWait WebDriverWait => webDriverWait ??= CreateDefaultWebDriverWait(WebDriver);
    protected WebDriverWait GlobalWebDriverWait => globalWebDriverWait ??= CreateDefaultWebDriverWait(GlobalWebDriver);
    protected virtual int DefaultWebDriverWaitTimeoutSeconds => 60;

    public void AssertCurrentActiveDefinedPageHasNoErrors(Assembly definedPageAssembly)
    {
        WebDriver.TryGetCurrentActiveDefinedPage(Settings, definedPageAssembly)?.AssertPageHasNoErrors();
    }

    protected virtual WebDriverWait CreateDefaultWebDriverWait(IWebDriver webDriver)
    {
        return new WebDriverWait(webDriver, DefaultWebDriverWaitTimeoutSeconds.Seconds())
            .With(p => p.PollingInterval = TimeSpan.FromMilliseconds(300));
    }
}

public abstract class TestCase : TestCase<AutomationTestSettings>
{
    protected TestCase(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        IScopedLazyWebDriver lazyWebDriver,
        ISingletonLazyWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
    }
}
