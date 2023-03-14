namespace Easy.Platform.AutomationTest.TestCases;

public interface ISpecFlowStepDefinitionsContext
{
}

public abstract class SpecFlowStepDefinitions<TSettings, TContext> : TestCase<TSettings>
    where TSettings : AutomationTestSettings
    where TContext : ISpecFlowStepDefinitionsContext
{
    protected SpecFlowStepDefinitions(
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

public abstract class SpecFlowStepDefinitions<TContext> : SpecFlowStepDefinitions<AutomationTestSettings, TContext>
    where TContext : ISpecFlowStepDefinitionsContext
{
    protected SpecFlowStepDefinitions(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver,
        TContext context) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver, context)
    {
    }
}
