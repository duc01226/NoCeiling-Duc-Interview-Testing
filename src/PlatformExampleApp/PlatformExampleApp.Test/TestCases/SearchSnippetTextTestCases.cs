using PlatformExampleApp.Test.Apps.TextSnippet.Pages;
using PlatformExampleApp.Test.TestCases.Helpers;

namespace PlatformExampleApp.Test.Apps.TextSnippet.TestCases;

[Trait("App", "TextSnippet")]
public class SearchSnippetTextTestCases : TestCase
{
    public SearchSnippetTextTestCases(
        IWebDriverManager driverManager,
        TestSettings settings,
        WebDriverLazyInitializer driverLazyInitializer,
        GlobalWebDriver globalWebDriver) : base(driverManager, settings, driverLazyInitializer, globalWebDriver)
    {
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void WHEN_SearchSnippetText_BY_CopyFirstItemTextAsSearchText()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = GlobalWebDriver.Value.NavigatePage<TextSnippetApp.HomePage>(Settings)
            .WaitInitLoadingDataSuccessWithFullPagingData(
                maxWaitForLoadingDataSeconds: Util.Random.ReturnByChanceOrDefault(
                    percentChance: 20, // random 20 percent test failed waiting timeout error by only one second
                    chanceReturnValue: 1,
                    defaultReturnValue: TextSnippetApp.HomePage.DefaultMaxRequestWaitSeconds));

        // WHEN: Copy snippet text in first grid row to search box
        var firstItemSnippetText = loadedHomePage
            .TextSnippetItemsGrid
            .GetCell(rowIndex: 0, colName: TextSnippetApp.HomePage.GripSnippetTextColName)!.RootElement!
            .Text;
        loadedHomePage.DoSearchTextSnippet(searchText: firstItemSnippetText);

        // THEN: At least one item matched with the search test displayed
        loadedHomePage.WaitUntilAssertSuccess(
            waitForSuccess: _ => _.AssertHasMatchingItemsForSearchText(firstItemSnippetText),
            stopIfFail: _ => _.AssertNoErrors());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void WHEN_SearchSnippetText_BY_NotExistingItemSearchText()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = GlobalWebDriver.Value.GetLoadingDataFinishedWithFullPagingDataHomePage(Settings);

        // WHEN: Search with random guid + "NotExistingItemSearchText"
        var searchText = "NotExistingItemSearchText" + Guid.NewGuid();
        loadedHomePage.DoSearchTextSnippet("NotExistingItemSearchText" + Guid.NewGuid());

        // THEN: No item is displayed
        loadedHomePage.WaitUntilAssertSuccess(
            waitForSuccess: _ => _.AssertNotHasMatchingItemsForSearchText(searchText),
            stopIfFail: _ => _.AssertNoErrors());
    }
}
