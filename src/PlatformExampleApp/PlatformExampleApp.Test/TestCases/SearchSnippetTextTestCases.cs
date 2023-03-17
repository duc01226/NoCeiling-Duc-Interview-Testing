using PlatformExampleApp.Test.Shared.Pages;
using PlatformExampleApp.Test.TestCases.Helpers;

namespace PlatformExampleApp.Test.TestCases;

[Trait(name: "App", value: "TextSnippet")]
public class SearchSnippetTextTestCases : TestCase
{
    public SearchSnippetTextTestCases(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        IScopedLazyWebDriver lazyWebDriver,
        ISingletonLazyWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
    }

    [Fact]
    [Trait(name: "Category", value: "Smoke")]
    public void WHEN_SearchSnippetText_BY_CopyFirstItemTextAsSearchText()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = GlobalWebDriver.NavigatePage<TextSnippetApp.HomePage>(Settings)
            .WaitInitLoadingDataSuccessWithFullPagingData(
                maxWaitForLoadingDataSeconds: Util.Random.ReturnByChanceOrDefault(
                    percentChance: 20, // random 20 percent test failed waiting timeout error by only one second
                    chanceReturnValue: 1,
                    TextSnippetApp.Const.DefaultMaxWaitSeconds));

        // WHEN: Copy snippet text in first grid row to search box
        var firstItemSnippetText = loadedHomePage.TextSnippetItemsTable
            .GetCell(rowIndex: 0, TextSnippetApp.HomePage.SnippetTextColName)!.Text;
        loadedHomePage.DoSearchTextSnippet(firstItemSnippetText);

        // THEN: At least one item matched with the search test displayed
        loadedHomePage.WaitUntilAssertSuccess(
            waitForSuccess: _ => _.AssertHasMatchingItemsForSearchText(firstItemSnippetText),
            continueWaitOnlyWhen: _ => _.AssertPageHasNoErrors());
    }

    [Fact]
    [Trait(name: "Category", value: "Smoke")]
    public void WHEN_SearchSnippetText_BY_NotExistingItemSearchText()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = GlobalWebDriver.GetLoadingDataFinishedWithFullPagingDataHomePage(Settings);

        // WHEN: Search with random guid + "NotExistingItemSearchText"
        var searchText = "NotExistingItemSearchText" + Guid.NewGuid();
        loadedHomePage.DoSearchTextSnippet(searchText: "NotExistingItemSearchText" + Guid.NewGuid());

        // THEN: No item is displayed
        loadedHomePage.WaitUntilAssertSuccess(
            waitForSuccess: _ => _.AssertNotHasMatchingItemsForSearchText(searchText),
            continueWaitOnlyWhen: _ => _.AssertPageHasNoErrors());
    }
}
