using PlatformExampleApp.Test.Apps.TextSnippet.Pages;
using PlatformExampleApp.Test.TestCases.Helpers;

namespace PlatformExampleApp.Test.TestCases.OthersLearningDemo;

/// <summary>
/// You could implement IDisposable for test case to dispose driver in dispose method and other disposable things <br/>
/// If you do not dispose in this case, the browser driver is not closed after test <br/>
/// THIS IS ONLY FOR LEARNING PURPOSE. USING DEPENDENCY INJECTION ALREADY DISPOSE DISPOSABLE OBJECT FOR US
/// </summary>
[Trait("App", "TextSnippet")]
public sealed class SearchSnippetTextTestCases_IDisposableDisposeObjectDemo : TestCase, IDisposable
{
    private readonly WebDriverLazyInitializer manuallyCreateDriverLazyInitializer;

    public SearchSnippetTextTestCases_IDisposableDisposeObjectDemo(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
        manuallyCreateDriverLazyInitializer = new WebDriverLazyInitializer(settings);
    }

    public void Dispose()
    {
        manuallyCreateDriverLazyInitializer.Dispose();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void WHEN_SearchSnippetText_BY_CopyFirstItemTextAsSearchText()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = manuallyCreateDriverLazyInitializer.Value.NavigatePage<TextSnippetApp.HomePage>(Settings)
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
            stopWaitOnAssertError: _ => _.AssertNoErrors());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void WHEN_SearchSnippetText_BY_NotExistingItemSearchText()
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = manuallyCreateDriverLazyInitializer.Value.GetLoadingDataFinishedWithFullPagingDataHomePage(Settings);

        // WHEN: Search with random guid + "NotExistingItemSearchText"
        var searchText = "NotExistingItemSearchText" + Guid.NewGuid();
        loadedHomePage.DoSearchTextSnippet("NotExistingItemSearchText" + Guid.NewGuid());

        // THEN: No item is displayed
        loadedHomePage.WaitUntilAssertSuccess(
            waitForSuccess: _ => _.AssertNotHasMatchingItemsForSearchText(searchText),
            stopWaitOnAssertError: _ => _.AssertNoErrors());
    }
}
