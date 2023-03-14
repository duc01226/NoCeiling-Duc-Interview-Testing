using PlatformExampleApp.Test.Apps.TextSnippet.Pages;
using PlatformExampleApp.Test.DataModels;

namespace PlatformExampleApp.Test.BDD.StepDefinitions;

public class CreateTestSnippetItemStepDefinitionsContext : ISpecFlowStepDefinitionsContext
{
    public TextSnippetApp.HomePage? GivenALoadSuccessHomePageResult { get; set; }
    public TextSnippetEntityData? WhenDoFillInAndSubmitRandomUniqueSaveSnippetTextFormResult { get; set; }
}

[Binding]
public class CreateTestSnippetItemStepDefinitions : SpecFlowStepDefinitions<CreateTestSnippetItemStepDefinitionsContext>
{
    public CreateTestSnippetItemStepDefinitions(
        IWebDriverManager driverManager,
        AutomationTestSettings settings,
        WebDriverLazyInitializer lazyWebDriver,
        GlobalWebDriver globalLazyWebDriver,
        CreateTestSnippetItemStepDefinitionsContext context) : base(driverManager, settings, lazyWebDriver, globalLazyWebDriver, context)
    {
    }

    [Given(@"a load success home page")]
    public void GivenALoadSuccessHomePage()
    {
        // GIVEN: loadedHomePage
        Context.GivenALoadSuccessHomePageResult = LazyWebDriver.Value.NavigatePage<TextSnippetApp.HomePage>(Settings)
            .WaitInitLoadingDataSuccessWithFullPagingData(
                maxWaitForLoadingDataSeconds: Util.Random.ReturnByChanceOrDefault(
                    percentChance: 20, // random 20 percent test failed waiting timeout error by only one second
                    chanceReturnValue: 1,
                    TextSnippetApp.HomePage.DefaultMaxRequestWaitSeconds));
    }

    [When(
        @"Fill in a new random unique value snippet text item data \(snippet text and full text\) and submit a new text snippet item, wait for submit request finished")]
    public void WhenDoFillInAndSubmitRandomUniqueSaveSnippetTextForm()
    {
        var loadedHomePage = Context.GivenALoadSuccessHomePageResult!;
        var autoRandomTextSnippetData = new TextSnippetEntityData("SnippetText" + Guid.NewGuid(), "FullText" + Guid.NewGuid());

        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(autoRandomTextSnippetData);

        Context.WhenDoFillInAndSubmitRandomUniqueSaveSnippetTextFormResult = autoRandomTextSnippetData;
    }

    [Then(@"Page show no errors")]
    public void ThenPageShowNoErrors()
    {
        Context.GivenALoadSuccessHomePageResult!.AssertPageNoErrors();
    }

    [Then(@"The item data should equal to the filled data when submit creating new text snippet item")]
    public void ThenTheItemDataShouldEqualToTheFilledDataWhenSubmitCreatingNewTextSnippetItem()
    {
        var newSnippetTextData = Context.WhenDoFillInAndSubmitRandomUniqueSaveSnippetTextFormResult!;

        Context.GivenALoadSuccessHomePageResult!.GetTextSnippetDataTableItems().First().Should().BeEquivalentTo(newSnippetTextData);
    }

    [When(@"Create a new random unique snippet text item successful and try create the same previous snippet text item value again")]
    public void WhenCreateANewRandomUniqueSnippetTextItemSuccessfulAndTryCreateTheSamePreviousSnippetTextItemValueAgain()
    {
        var loadedHomePage = Context.GivenALoadSuccessHomePageResult!;
        var autoRandomTextSnippetData = new TextSnippetEntityData("SnippetText" + Guid.NewGuid(), "FullText" + Guid.NewGuid());

        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(autoRandomTextSnippetData);
        loadedHomePage.DoFillInAndSubmitSaveSnippetTextForm(autoRandomTextSnippetData);
    }

    [Then(@"Page must show create duplicated snippet text errors")]
    public void ThenPageMustShowErrors()
    {
        Context.GivenALoadSuccessHomePageResult!.AssertPageMustHasCreateDuplicatedSnippetTextError();
    }

    [Then(@"Do search text snippet item with the snippet text that has just being created success must found exact one match item in the table for the search text")]
    public void ThenDoSearchTextSnippetItemWithTheSnippetTextThatHasJustBeingCreatedSuccessMustFoundExactOneMatchItemInTheTableForTheSearchText()
    {
        var newSnippetTextData = Context.WhenDoFillInAndSubmitRandomUniqueSaveSnippetTextFormResult!;

        Context.GivenALoadSuccessHomePageResult!.DoSearchTextSnippet(newSnippetTextData.SnippetText)
            .WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertHasExactMatchItemForSearchText(newSnippetTextData.SnippetText),
                stopWaitOnAssertError: _ => _.AssertPageNoErrors());
    }
}
