using AutoFixture.Xunit2;
using PlatformExampleApp.Test.Apps.TextSnippet.Pages;
using PlatformExampleApp.Test.DataModels;

namespace PlatformExampleApp.Test.Apps.TextSnippet.TestCases;

[Trait(name: "App", value: "TextSnippet")]
public class CreateNewSnippetTextTestCases : TestCase
{
    public CreateNewSnippetTextTestCases(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
    }

    // autoRandomTextSnippetData is auto generated from AutoData attribute
    [Theory]
    [AutoData]
    [Trait(name: "Category", value: "Smoke")]
    public void WHEN_CreateNewSnippetText_BY_DifferentValidUniqueName(TextSnippetEntityData autoRandomTextSnippetEntityData)
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = LazyWebDriver.Value.NavigatePage<TextSnippetApp.HomePage>(Settings)
            .WaitInitLoadingDataSuccessWithFullPagingData(
                maxWaitForLoadingDataSeconds: Util.Random.ReturnByChanceOrDefault(
                    percentChance: 20, // random 20 percent test failed waiting timeout error by only one second
                    chanceReturnValue: 1,
                    TextSnippetApp.HomePage.DefaultMaxRequestWaitSeconds));

        // WHEN: Create new item snippet text by different unique name
        var newSnippetText = autoRandomTextSnippetEntityData.SnippetText;
        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(autoRandomTextSnippetEntityData);

        // THEN: SnippetText item is created with no errors and item could be searched
        loadedHomePage.AssertPageNoErrors();
        loadedHomePage.DoSearchTextSnippet(newSnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertHasExactMatchItemForSearchText(newSnippetText),
                stopWaitOnAssertError: _ => _.AssertPageNoErrors());
        loadedHomePage.GetTextSnippetDataTableItems().First().Should().BeEquivalentTo(autoRandomTextSnippetEntityData);
    }
}
