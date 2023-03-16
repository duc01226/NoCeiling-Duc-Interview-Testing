namespace Easy.Platform.AutomationTest.TestCases;

public abstract class BddStepDefinitions<TSettings, TContext> : TestCase<TSettings>
    where TSettings : AutomationTestSettings
    where TContext : IBddStepsContext
{
    protected BddStepDefinitions(
        IWebDriverManager driverManager,
        TSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver,
        TContext context) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
        Context = context;
    }

    public TContext Context { get; }
}

public abstract class BddStepDefinitions<TContext> : BddStepDefinitions<AutomationTestSettings, TContext>
    where TContext : IBddStepsContext
{
    protected BddStepDefinitions(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver,
        TContext context) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver, context)
    {
    }
}

public interface IBddStepsContext
{
}
