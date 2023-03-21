using PlatformExampleApp.Test.Shared.EntityData;
using PlatformExampleApp.Test.Shared.Pages;

namespace PlatformExampleApp.Test.BDD.StepDefinitions;

public class HomePageStepsContext : IBddStepsContext
{
    public TextSnippetApp.HomePage? LoadedSuccessHomePage { get; set; }
    public TextSnippetEntityData? DoFillInAndSubmitRandomUniqueSnippetTextData { get; set; }
}

[Binding]
public class HomePageSteps : BddStepDefinitions<TextSnippetAutomationTestSettings, HomePageStepsContext>
{
    public HomePageSteps(
        IWebDriverManager driverManager,
        TextSnippetAutomationTestSettings settings,
        IScopedLazyWebDriver lazyWebDriver,
        ISingletonLazyWebDriver globalLazyWebDriver,
        HomePageStepsContext context) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver, context)
    {
    }

    [Given(@"Loaded success home page")]
    public void GivenALoadedSuccessHomePage()
    {
        Context.LoadedSuccessHomePage = WebDriver.NavigatePage<TextSnippetApp.HomePage>(Settings)
            .WaitInitLoadingDataSuccessWithFullPagingData(
                maxWaitForLoadingDataSeconds: Settings.RandomTestShortWaitingFailed == true
                    ? Util.Random.ReturnByChanceOrDefault(
                        percentChance: 20, // random 20 percent test failed waiting timeout error by only one second
                        chanceReturnValue: 1,
                        TextSnippetApp.Const.DefaultMaxWaitSeconds)
                    : TextSnippetApp.Const.DefaultMaxWaitSeconds);
    }

    [When(
        @"Fill in a new random unique value snippet text item data \(snippet text and full text\) and submit a new text snippet item, wait for submit request finished")]
    public void WhenDoFillInAndSubmitRandomUniqueSaveSnippetTextForm()
    {
        var loadedHomePage = Context.LoadedSuccessHomePage!;
        var autoRandomTextSnippetData = new TextSnippetEntityData("SnippetText" + Guid.NewGuid(), "FullText" + Guid.NewGuid());

        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(autoRandomTextSnippetData);

        Context.DoFillInAndSubmitRandomUniqueSnippetTextData = autoRandomTextSnippetData;
    }

    [Then(@"The item data should equal to the filled data when submit creating new text snippet item")]
    public void ThenTheItemDataShouldEqualToTheFilledDataWhenSubmitCreatingNewTextSnippetItem()
    {
        var newSnippetTextData = Context.DoFillInAndSubmitRandomUniqueSnippetTextData!;

        Context.LoadedSuccessHomePage!.GetTextSnippetDataTableItems().First().Should().BeEquivalentTo(newSnippetTextData);
    }

    [When(@"Create a new random unique snippet text item successful and try create the same previous snippet text item value again")]
    public void WhenCreateANewRandomUniqueSnippetTextItemSuccessfulAndTryCreateTheSamePreviousSnippetTextItemValueAgain()
    {
        var loadedHomePage = Context.LoadedSuccessHomePage!;
        var autoRandomTextSnippetData = new TextSnippetEntityData("SnippetText".ToUniqueStr(), "FullText".ToUniqueStr());

        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(autoRandomTextSnippetData);
        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(autoRandomTextSnippetData);
    }

    [Then(@"Page must show create duplicated snippet text errors")]
    public void ThenPageMustShowErrors()
    {
        Context.LoadedSuccessHomePage!.AssertPageMustHasCreateDuplicatedSnippetTextError();
    }

    [Then(@"Do search text snippet item with the snippet text that has just being created success must found exact one match item in the table for the search text")]
    public void ThenDoSearchTextSnippetItemWithTheSnippetTextThatHasJustBeingCreatedSuccessMustFoundExactOneMatchItemInTheTableForTheSearchText()
    {
        var newSnippetTextData = Context.DoFillInAndSubmitRandomUniqueSnippetTextData!;

        Context.LoadedSuccessHomePage!.DoSearchTextSnippet(newSnippetTextData.SnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertHasExactMatchItemForSearchText(newSnippetTextData.SnippetText),
                continueWaitOnlyWhen: _ => _.AssertPageHasNoErrors());
    }
}
