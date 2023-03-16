using PlatformExampleApp.Test.Shared.Pages;

namespace PlatformExampleApp.Test.BDD.StepDefinitions.Common;

public class CommonStepsContext : IBddStepsContext
{
}

[Binding]
public class CommonSteps : BddStepDefinitions<TextSnippetAutomationTestSettings, CommonStepsContext>
{
    public CommonSteps(
        IWebDriverManager driverManager,
        TextSnippetAutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver,
        CommonStepsContext context)
        : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver, context)
    {
    }

    [Then(@"Current page has no errors")]
    public void ThenPageShowNoErrors()
    {
        AssertCurrentActiveDefinedPageHasNoErrors(typeof(TextSnippetApp).Assembly);
    }
}
