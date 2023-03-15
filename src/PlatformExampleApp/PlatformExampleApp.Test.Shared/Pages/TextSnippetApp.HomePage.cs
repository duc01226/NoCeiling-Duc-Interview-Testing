using PlatformExampleApp.Test.Shared.CommonUiComponents;
using PlatformExampleApp.Test.Shared.EntityData;

namespace PlatformExampleApp.Test.Shared.Pages;

public static partial class TextSnippetApp
{
    public class HomePage : BasePage<HomePage>
    {
        public const string DefaultTitle = "Playground TextSnippet";
        public const int TextSnippetItemsTablePageSize = 10;
        public static readonly string SnippetTextColName = "SnippetText";
        public static readonly string FullTextColName = "FullText (Click on row to see detail)";

        public HomePage(IWebDriver webDriver, AutomationTestSettings settings) : base(webDriver, settings)
        {
            SearchTextSnippetTxt = new FormFieldUiComponent(webDriver, directReferenceRootElement: null, parent: this)
                .WithIdentifierSelector(appSearchInput: ".app__search-input");
            SaveSnippetFormSnippetTextTxt = new FormFieldUiComponent(webDriver, directReferenceRootElement: null, parent: this)
                .WithIdentifierSelector(appSearchInput: ".text-snippet-detail__snippet-text-form-field");
            SaveSnippetFormFullTextTxt = new FormFieldUiComponent(webDriver, directReferenceRootElement: null, parent: this)
                .WithIdentifierSelector(appSearchInput: ".text-snippet-detail__full-text-form-field");
        }

        public override string Path => "";
        public override string Title => DefaultTitle;

        public IWebElement? Header => WebDriver.TryFindElement(cssSelector: ".app__header > h1");

        public IWebElement? GlobalError =>
            WebDriver.TryFindElement(cssSelector: ".app__errors-content")
                .PipeIfOrDefault(when: element => element?.Text.IsNullOrWhiteSpace() == false, thenPipe: element => element, defaultValue: null);

        public List<IWebElement> SaveSnippetTextDetailErrors =>
            WebDriver.FindElements(cssSelector: "platform-example-web-text-snippet-detail .text-snippet-detail__error");

        public HtmlTableUiComponent TextSnippetItemsTable => new(WebDriver, rootElementClassSelector: ".app__text-snippet-items-grid > table", parent: this);

        public FormFieldUiComponent SearchTextSnippetTxt { get; }

        public FormFieldUiComponent SaveSnippetFormSnippetTextTxt { get; }
        public FormFieldUiComponent SaveSnippetFormFullTextTxt { get; }
        public GeneralUiComponent SaveSnippetFormSubmitBtn => new(WebDriver, rootElementClassSelector: ".text-snippet-detail__main-form-submit-btn", parent: this);
        public GeneralUiComponent SaveSnippetFormResetBtn => new(WebDriver, rootElementClassSelector: ".text-snippet-detail__main-form-reset-btn", parent: this);

        public override List<IWebElement> AllErrors()
        {
            return base.AllErrors().ConcatIf(@if: GlobalError != null, GlobalError!).Concat(SaveSnippetTextDetailErrors).ToList();
        }

        public HomePage AssertTextSnippetItemsDisplayFullPage()
        {
            return this.AssertMust(
                must: _ => TextSnippetItemsTable.Rows.Count == TextSnippetItemsTablePageSize,
                expected: $"TextSnippetItemsTable.Rows.Count: {TextSnippetItemsTablePageSize}",
                actual: $"TextSnippetItemsTable.Rows.Count: {TextSnippetItemsTable.Rows.Count}");
        }

        public HomePage WaitInitLoadingDataSuccessWithFullPagingData(
            int maxWaitForLoadingDataSeconds = DefaultMaxRequestWaitSeconds)
        {
            AssertPageDocumentLoaded();

            WaitGlobalSpinnerStopped(maxWaitForLoadingDataSeconds);

            AssertPageNoErrors().AssertTextSnippetItemsDisplayFullPage();

            return this;
        }

        public HomePage DoSearchTextSnippet(
            string searchText,
            int maxWaitForLoadingDataSeconds = DefaultMaxRequestWaitSeconds)
        {
            SearchTextSnippetTxt.ReplaceTextAndEnter(searchText);

            // Do RetryOnException for CheckAllTextSnippetGrowsMatchSearchText because
            // it access list element from filter, which could be stale because data is filtered, element lost
            // when it's checking the element matching
            this.WaitUntil(
                condition: _ => ValidateNoErrors() == false ||
                                Util.TaskRunner.WaitRetryThrowFinalException<bool, StaleElementReferenceException>(
                                    executeFunc: () => CheckAllTextSnippetGrowsMatchSearchText(searchText)),
                maxWaitForLoadingDataSeconds,
                waitForMsg: "TextSnippetItemsTable search items data is finished.");

            return this;
        }

        public HomePage AssertHasExactMatchItemForSearchText(string searchText)
        {
            AssertPageNoErrors();

            this.AssertMust(
                must: _ => TextSnippetItemsTable.Rows.Count == 1 &&
                           TextSnippetItemsTable.Rows.Any(predicate: row => row.GetCell(SnippetTextColName)!.RootElement!.Text == searchText),
                expected: $"GridRowSnippetTextValues contains at least one item equal '{searchText}'",
                actual: $"GridRowSnippetTextValues: {GetTextSnippetDataTableItems().Select(p => p.SnippetText).AsFormattedJson()}");

            return this;
        }

        public string DoSelectTextSnippetItemToEditInForm(int itemIndex)
        {
            TextSnippetItemsTable.Rows[itemIndex].Click();

            var selectedItemSnippetText = TextSnippetItemsTable.Rows[itemIndex].GetCell(SnippetTextColName)!.RootElement!.Text;

            // Wait for data is loaded into SaveSnippetText form
            WaitUntilAssertSuccess(
                waitForSuccess: _ => _.AssertMust(
                    must: _ => _.SaveSnippetFormSnippetTextTxt.Value == selectedItemSnippetText,
                    expected: $"SaveSnippetFormSnippetTextTxt.Value must be '{selectedItemSnippetText}'",
                    actual: $"{_.SaveSnippetFormSnippetTextTxt.Value}"),
                stopWaitOnAssertError: _ => _.AssertPageNoErrors());

            return selectedItemSnippetText;
        }

        public HomePage AssertNotHasMatchingItemsForSearchText(string searchText)
        {
            AssertPageNoErrors();

            this.AssertMust(
                must: _ => TextSnippetItemsTable.Rows.Count == 0,
                expected: $"TextSnippetItemsTable.Rows.Count must equal 0 for searchText '{searchText}'",
                actual: $"GridRowSnippetTextValues: {GetTextSnippetDataTableItems().Select(p => p.SnippetText).AsFormattedJson()}");

            return this;
        }

        public HomePage AssertHasMatchingItemsForSearchText(string searchText)
        {
            AssertPageNoErrors();

            this.AssertMust(
                must: _ => TextSnippetItemsTable.Rows.Count >= 1 &&
                           CheckAllTextSnippetGrowsMatchSearchText(searchText),
                expected: $"GridRowSnippetTextValues contains at least one item match '{searchText}'",
                actual: $"GridRowSnippetTextValues: {GetTextSnippetDataTableItems().Select(p => p.SnippetText).AsFormattedJson()}");

            return this;
        }

        public HomePage AssertNotHasExactMatchItemForSearchText(string searchText)
        {
            AssertPageNoErrors();

            this.AssertMust(
                must: _ => GetTextSnippetDataTableItems().Select(p => p.SnippetText).All(predicate: rowSnippetTextValue => rowSnippetTextValue != searchText),
                expected: $"SnippetText Item '{searchText}' must not existing",
                actual: $"GridRowSnippetTextValues: {GetTextSnippetDataTableItems().Select(p => p.SnippetText).AsFormattedJson()}");

            return this;
        }

        public bool CheckAllTextSnippetGrowsMatchSearchText(string searchText)
        {
            var searchWords = searchText.Split(separator: " ").Where(predicate: word => !word.IsNullOrWhiteSpace()).ToList();

            return GetTextSnippetDataTableItems()
                .Select(p => p.SnippetText)
                .All(
                    predicate: rowSnippetTextValue => searchWords.Any(
                        predicate: searchWord => rowSnippetTextValue.Contains(searchWord, StringComparison.InvariantCultureIgnoreCase)));
        }

        public HomePage DoFillInAndSubmitSaveSnippetTextForm(TextSnippetEntityData textSnippetEntityData)
        {
            SaveSnippetFormSubmitBtn
                .WaitAndRetryUntil(
                    action: _ =>
                    {
                        SaveSnippetFormSnippetTextTxt.ReplaceTextAndEnter(textSnippetEntityData.SnippetText);
                        SaveSnippetFormFullTextTxt.ReplaceTextAndEnter(textSnippetEntityData.FullText);
                    },
                    until: _ => _.IsClickable(),
                    maxWaitSeconds: 3,
                    waitForMsg: "SaveSnippetFormSubmitBtn is clickable")
                .Click();

            WaitGlobalSpinnerStopped(
                DefaultMaxRequestWaitSeconds,
                waitForMsg: "Wait for saving snippet text successfully");

            return this;
        }

        public List<TextSnippetEntityData> GetTextSnippetDataTableItems()
        {
            return TextSnippetItemsTable.Rows
                .Select(
                    p => new TextSnippetEntityData(
                        snippetText: p.GetCell(SnippetTextColName)!.RootElement!.Text,
                        fulltext: p.GetCell(FullTextColName)!.RootElement!.Text))
                .ToList();
        }

        public HomePage AssertPageMustHasCreateDuplicatedSnippetTextError()
        {
            return AssertPageMustHasErrors(TextSnippetEntityData.Errors.DuplicatedSnippetTextErrorMsg);
        }
    }
}
