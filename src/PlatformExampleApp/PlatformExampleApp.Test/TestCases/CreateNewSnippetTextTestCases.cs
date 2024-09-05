using AutoFixture.Xunit2;
using PlatformExampleApp.Test.Shared.EntityData;
using PlatformExampleApp.Test.Shared.Pages;

namespace PlatformExampleApp.Test.TestCases;

[Trait(name: "App", value: "TextSnippet")]
public class CreateNewSnippetTextTestCases : TestCase<TextSnippetAutomationTestSettings>
{
    public CreateNewSnippetTextTestCases(
        IWebDriverManager driverManager,
        TextSnippetAutomationTestSettings settings,
        IScopedLazyWebDriver lazyWebDriver,
        ISingletonLazyWebDriver globalLazyWebDriver) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver)
    {
    }

    // autoRandomTextSnippetData is auto generated from AutoData attribute
    [Theory]
    [AutoData]
    [Trait(name: "Category", value: "Smoke")]
    public void WHEN_CreateNewSnippetText_BY_DifferentValidUniqueName(TextSnippetEntityData autoRandomTextSnippetEntityData)
    {
        // GIVEN: loadedHomePage
        var loadedHomePage = WebDriver.NavigatePage<TextSnippetApp.HomePage>(Settings)
            .WaitInitLoadingDataSuccessWithFullPagingData(
                maxWaitForLoadingDataSeconds: Settings.RandomTestShortWaitingFailed == true
                    ? Util.RandomGenerator.ReturnByChanceOrDefault(
                        percentChance: 20, // random 20 percent test failed waiting timeout error by only one second
                        chanceReturnValue: 1,
                        TextSnippetApp.Const.DefaultMaxWaitSeconds)
                    : TextSnippetApp.Const.DefaultMaxWaitSeconds);

        // WHEN: Create new item snippet text by different unique name
        var newSnippetText = autoRandomTextSnippetEntityData.SnippetText;
        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(autoRandomTextSnippetEntityData);

        // THEN: SnippetText item is created with no errors and item could be searched
        loadedHomePage.AssertPageHasNoErrors();
        loadedHomePage.DoSearchTextSnippet(newSnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: p => p.AssertHasExactMatchItemForSearchText(newSnippetText),
                continueWaitOnlyWhen: p => p.AssertPageHasNoErrors());
        loadedHomePage.GetTextSnippetDataTableItems().First().Should().BeEquivalentTo(autoRandomTextSnippetEntityData);
    }
}
