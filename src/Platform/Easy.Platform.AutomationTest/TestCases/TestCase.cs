namespace Easy.Platform.AutomationTest.TestCases;

public abstract class TestCase<TSettings> where TSettings : TestSettings
{
    protected TestCase(IWebDriverManager driverManager, TSettings settings, WebDriverLazyInitializer driverLazyInitializer, GlobalWebDriver globalWebDriver)
    {
        DriverManager = driverManager;
        Settings = settings;
        DriverInitializer = driverLazyInitializer;
        GlobalWebDriver = globalWebDriver;
    }

    protected IWebDriverManager DriverManager { get; set; }
    protected TSettings Settings { get; set; }
    protected WebDriverLazyInitializer DriverInitializer { get; set; }
    protected GlobalWebDriver GlobalWebDriver { get; }
}

public abstract class TestCase : TestCase<TestSettings>
{
    protected TestCase(
        IWebDriverManager driverManager,
        TestSettings settings,
        WebDriverLazyInitializer driverLazyInitializer,
        GlobalWebDriver globalWebDriver) : base(driverManager, settings, driverLazyInitializer, globalWebDriver)
    {
    }
}
