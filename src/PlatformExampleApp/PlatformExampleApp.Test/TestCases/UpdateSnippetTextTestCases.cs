using PlatformExampleApp.Test.DataModels;
using PlatformExampleApp.Test.TestCases.Helpers;

namespace PlatformExampleApp.Test.Apps.TextSnippet.TestCases;

[Trait("App", "TextSnippet")]
public class UpdateSnippetTextTestCases : TestCase
{
    public UpdateSnippetTextTestCases(
        IWebDriverManager driverManager,
        TestSettings settings,
        WebDriverLazyInitializer driverLazyInitializer,
        GlobalWebDriver globalWebDriver) : base(driverManager, settings, driverLazyInitializer, globalWebDriver)
    {
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void WHEN_UpdateSnippetText_BY_DifferentValidUniqueName()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = DriverInitializer.Value.GetLoadingDataFinishedWithFullPagingDataHomePage(Settings);

        // WHEN: Update first item snippet text by different valid unique name
        var beforeUpdateFirstItemSnippetText = loadedHomePage.DoSelectTextSnippetItemToEditInForm(0);
        var toUpdateSnippetText = "WHEN_UpdateSnippetText " + Guid.NewGuid();
        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(new TextSnippetData(toUpdateSnippetText, toUpdateSnippetText + " FullText"));

        // THEN: SnippetText item is updated with no errors, old value couldn't be searched and new updated value could be searched
        loadedHomePage.AssertNoErrors();
        loadedHomePage.DoSearchTextSnippet(beforeUpdateFirstItemSnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertNotHasExactMatchItemForSearchText(beforeUpdateFirstItemSnippetText),
                stopIfFail: _ => _.AssertNoErrors());
        loadedHomePage.DoSearchTextSnippet(toUpdateSnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertHasExactMatchItemForSearchText(toUpdateSnippetText),
                stopIfFail: _ => _.AssertNoErrors());
    }
}
