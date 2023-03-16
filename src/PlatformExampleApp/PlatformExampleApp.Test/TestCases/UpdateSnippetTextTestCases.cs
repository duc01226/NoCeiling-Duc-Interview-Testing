using PlatformExampleApp.Test.Shared.EntityData;
using PlatformExampleApp.Test.TestCases.Helpers;

namespace PlatformExampleApp.Test.TestCases;

[Trait(name: "App", value: "TextSnippet")]
public class UpdateSnippetTextTestCases : TestCase
{
    public UpdateSnippetTextTestCases(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
    }

    [Fact]
    [Trait(name: "Category", value: "Smoke")]
    public void WHEN_UpdateSnippetText_BY_DifferentValidUniqueName()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = LazyWebDriver.Value.GetLoadingDataFinishedWithFullPagingDataHomePage(Settings);

        // WHEN: Update first item snippet text by different valid unique name
        var beforeUpdateFirstItemSnippetText = loadedHomePage.DoSelectTextSnippetItemToEditInForm(itemIndex: 0);
        var toUpdateSnippetText = "WHEN_UpdateSnippetText " + Guid.NewGuid();
        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(
            textSnippetEntityData: new TextSnippetEntityData(toUpdateSnippetText, fulltext: toUpdateSnippetText + " FullText"));

        // THEN: SnippetText item is updated with no errors, old value couldn't be searched and new updated value could be searched
        loadedHomePage.AssertPageHasNoErrors();
        loadedHomePage.DoSearchTextSnippet(beforeUpdateFirstItemSnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertNotHasExactMatchItemForSearchText(beforeUpdateFirstItemSnippetText),
                continueWaitOnlyWhen: _ => _.AssertPageHasNoErrors());
        loadedHomePage.DoSearchTextSnippet(toUpdateSnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertHasExactMatchItemForSearchText(toUpdateSnippetText),
                continueWaitOnlyWhen: _ => _.AssertPageHasNoErrors());
    }
}
