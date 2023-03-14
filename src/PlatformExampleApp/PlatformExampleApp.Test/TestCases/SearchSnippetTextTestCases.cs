using PlatformExampleApp.Test.Apps.TextSnippet.Pages;
using PlatformExampleApp.Test.TestCases.Helpers;

namespace PlatformExampleApp.Test.Apps.TextSnippet.TestCases;

[Trait(name: "App", value: "TextSnippet")]
public class SearchSnippetTextTestCases : TestCase
{
    public SearchSnippetTextTestCases(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
    }

    [Fact]
    [Trait(name: "Category", value: "Smoke")]
    public void WHEN_SearchSnippetText_BY_CopyFirstItemTextAsSearchText()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = GlobalLazyWebDriver.Value.NavigatePage<TextSnippetApp.HomePage>(Settings)
            .WaitInitLoadingDataSuccessWithFullPagingData(
                maxWaitForLoadingDataSeconds: Util.Random.ReturnByChanceOrDefault(
                    percentChance: 20, // random 20 percent test failed waiting timeout error by only one second
                    chanceReturnValue: 1,
                    TextSnippetApp.HomePage.DefaultMaxRequestWaitSeconds));

        // WHEN: Copy snippet text in first grid row to search box
        var firstItemSnippetText = loadedHomePage
            .TextSnippetItemsTable
            .GetCell(rowIndex: 0, TextSnippetApp.HomePage.SnippetTextColName)!.RootElement!
            .Text;
        loadedHomePage.DoSearchTextSnippet(firstItemSnippetText);

        // THEN: At least one item matched with the search test displayed
        loadedHomePage.WaitUntilAssertSuccess(
            waitForSuccess: _ => _.AssertHasMatchingItemsForSearchText(firstItemSnippetText),
            stopWaitOnAssertError: _ => _.AssertPageNoErrors());
    }

    [Fact]
    [Trait(name: "Category", value: "Smoke")]
    public void WHEN_SearchSnippetText_BY_NotExistingItemSearchText()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = GlobalLazyWebDriver.Value.GetLoadingDataFinishedWithFullPagingDataHomePage(Settings);

        // WHEN: Search with random guid + "NotExistingItemSearchText"
        var searchText = "NotExistingItemSearchText" + Guid.NewGuid();
        loadedHomePage.DoSearchTextSnippet(searchText: "NotExistingItemSearchText" + Guid.NewGuid());

        // THEN: No item is displayed
        loadedHomePage.WaitUntilAssertSuccess(
            waitForSuccess: _ => _.AssertNotHasMatchingItemsForSearchText(searchText),
            stopWaitOnAssertError: _ => _.AssertPageNoErrors());
    }
}
