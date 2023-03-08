namespace Easy.Platform.AutomationTest.TestCases;

public abstract class TestCase<TSettings> where TSettings : AutomationTestSettings
{
    protected TestCase(IWebDriverManager driverManager, TSettings settings, WebDriverLazyInitializer lazyWebDriver, GlobalWebDriver globalLazyWebDriver)
    {
        DriverManager = driverManager;
        Settings = settings;
        LazyWebDriver = lazyWebDriver;
        GlobalLazyWebDriver = globalLazyWebDriver;
    }

    protected IWebDriverManager DriverManager { get; set; }
    protected TSettings Settings { get; set; }
    protected WebDriverLazyInitializer LazyWebDriver { get; set; }
    protected GlobalWebDriver GlobalLazyWebDriver { get; }
}

public abstract class TestCase : TestCase<AutomationTestSettings>
{
    protected TestCase(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
    }
}
