using PlatformExampleApp.Test.Apps.TextSnippet.Pages;
using PlatformExampleApp.Test.DataModels;

namespace PlatformExampleApp.Test.Apps.TextSnippet.TestCases;

[Trait("App", "TextSnippet")]
public class CreateNewSnippetTextTestCases : TestCase
{
    public CreateNewSnippetTextTestCases(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void WHEN_CreateNewSnippetText_BY_DifferentValidUniqueName()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = LazyWebDriver.Value.NavigatePage<TextSnippetApp.HomePage>(Settings)
            .WaitInitLoadingDataSuccessWithFullPagingData(
                maxWaitForLoadingDataSeconds: Util.Random.ReturnByChanceOrDefault(
                    percentChance: 20, // random 20 percent test failed waiting timeout error by only one second
                    chanceReturnValue: 1,
                    defaultReturnValue: TextSnippetApp.HomePage.DefaultMaxRequestWaitSeconds));

        // WHEN: Update first item snippet text by different unique name
        var newSnippetText = "WHEN_CreateNewSnippetText " + Guid.NewGuid();
        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(new TextSnippetData(newSnippetText, newSnippetText + " FullText"));

        // THEN: SnippetText item is created with no errors and item could be searched
        loadedHomePage.AssertNoErrors();
        loadedHomePage.DoSearchTextSnippet(newSnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertHasExactMatchItemForSearchText(newSnippetText),
                stopWaitOnExceptionOrAssertFailed: _ => _.AssertNoErrors());
    }
}
