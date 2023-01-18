using PlatformExampleApp.Test.CommonUiComponents;
using PlatformExampleApp.Test.DataModels;

namespace PlatformExampleApp.Test.Apps.TextSnippet.Pages;

public static partial class TextSnippetApp
{
    public class HomePage : BasePage<HomePage>
    {
        public const string DefaultTitle = "Playground TextSnippet";
        public const int TextSnippetItemsGridPageSize = 10;
        public static readonly string GripSnippetTextColName = "SnippetText";

        public HomePage(IWebDriver webDriver, TestSettings settings) : base(webDriver, settings)
        {
            SearchTextSnippetTxt = new FormFieldUiComponent(webDriver, null, this)
                .WithIdentifierSelector(".app__search-input");
            SaveSnippetFormSnippetTextTxt = new FormFieldUiComponent(webDriver, null, this)
                .WithIdentifierSelector(".text-snippet-detail__snippet-text-form-field");
            SaveSnippetFormFullTextTxt = new FormFieldUiComponent(webDriver, null, this)
                .WithIdentifierSelector(".text-snippet-detail__full-text-form-field");
        }

        public override string Path => "";
        public override string Title => DefaultTitle;

        public IWebElement? Header => WebDriver.TryFindElement(".app__header > h1");

        public IWebElement? GlobalError =>
            WebDriver.TryFindElement(".app__errors-content")
                .PipeIfOrDefault(element => element?.Text.IsNullOrWhiteSpace() == false, element => element, defaultValue: null);

        public HtmlTableUiComponent TextSnippetItemsGrid => new(WebDriver, ".app__text-snippet-items-grid > table", this);

        public FormFieldUiComponent SearchTextSnippetTxt { get; }

        public FormFieldUiComponent SaveSnippetFormSnippetTextTxt { get; }
        public FormFieldUiComponent SaveSnippetFormFullTextTxt { get; }
        public GeneralUiComponent SaveSnippetFormSubmitBtn => new(WebDriver, ".text-snippet-detail__main-form-submit-btn", this);
        public GeneralUiComponent SaveSnippetFormResetBtn => new(WebDriver, ".text-snippet-detail__main-form-reset-btn", this);

        public override List<IWebElement> AllErrors()
        {
            return base.AllErrors().ConcatIf(GlobalError != null, GlobalError!).ToList();
        }

        public HomePage AssertTextSnippetItemsDisplayFullPage()
        {
            return this.AssertMust(
                must: _ => TextSnippetItemsGrid.Rows.Count == TextSnippetItemsGridPageSize,
                expected: $"TextSnippetItemsGrid.Rows.Count: {TextSnippetItemsGridPageSize}",
                actual: $"TextSnippetItemsGrid.Rows.Count: {TextSnippetItemsGrid.Rows.Count}");
        }

        public HomePage WaitInitLoadingDataSuccessWithFullPagingData(
            int maxWaitForLoadingDataSeconds = DefaultMaxRequestWaitSeconds)
        {
            AssertPageDocumentLoaded();

            WaitGlobalSpinnerStopped(maxWaitForLoadingDataSeconds);

            AssertNoErrors().AssertTextSnippetItemsDisplayFullPage();

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
                _ => ValidateNoErrors() == false ||
                     Util.TaskRunner.WaitRetryThrowFinalException<bool, StaleElementReferenceException>(
                         () => CheckAllTextSnippetGrowsMatchSearchText(searchText)),
                maxWaitSeconds: maxWaitForLoadingDataSeconds,
                waitForMsg: "TextSnippetItemsGrid search items data is finished.");

            return this;
        }

        public HomePage AssertHasExactMatchItemForSearchText(string searchText)
        {
            AssertNoErrors();

            this.AssertMust(
                must: _ => TextSnippetItemsGrid.Rows.Count == 1 &&
                           TextSnippetItemsGrid.Rows.Any(row => row.GetCell(colName: GripSnippetTextColName)!.RootElement!.Text == searchText),
                expected: $"GridRowSnippetTextValues contains at least one item equal '{searchText}'",
                actual: $"GridRowSnippetTextValues: {GridRowSnippetTextValues().AsFormattedJson()}");

            return this;
        }

        public string DoSelectTextSnippetItemToEditInForm(int itemIndex)
        {
            TextSnippetItemsGrid.Rows[itemIndex].Click();

            var selectedItemSnippetText = TextSnippetItemsGrid.Rows[itemIndex].GetCell(colName: GripSnippetTextColName)!.RootElement!.Text;

            // Wait for data is loaded into SaveSnippetText form
            WaitUntilAssertSuccess(
                _ => _.AssertMust(
                    _ => _.SaveSnippetFormSnippetTextTxt.Value == selectedItemSnippetText,
                    expected: $"SaveSnippetFormSnippetTextTxt.Value must be '{selectedItemSnippetText}'",
                    actual: $"{_.SaveSnippetFormSnippetTextTxt.Value}"),
                stopIfFail: _ => _.AssertNoErrors());

            return selectedItemSnippetText;
        }

        public HomePage AssertNotHasMatchingItemsForSearchText(string searchText)
        {
            AssertNoErrors();

            this.AssertMust(
                must: _ => TextSnippetItemsGrid.Rows.Count == 0,
                expected: $"TextSnippetItemsGrid.Rows.Count must equal 0 for searchText '{searchText}'",
                actual: $"GridRowSnippetTextValues: {GridRowSnippetTextValues().AsFormattedJson()}");

            return this;
        }

        public HomePage AssertHasMatchingItemsForSearchText(string searchText)
        {
            AssertNoErrors();

            this.AssertMust(
                must: _ => TextSnippetItemsGrid.Rows.Count >= 1 &&
                           CheckAllTextSnippetGrowsMatchSearchText(searchText: searchText),
                expected: $"GridRowSnippetTextValues contains at least one item match '{searchText}'",
                actual: $"GridRowSnippetTextValues: {GridRowSnippetTextValues().AsFormattedJson()}");

            return this;
        }

        public HomePage AssertNotHasExactMatchItemForSearchText(string searchText)
        {
            AssertNoErrors();

            this.AssertMust(
                must: _ => GridRowSnippetTextValues().All(rowSnippetTextValue => rowSnippetTextValue != searchText),
                expected: $"SnippetText Item '{searchText}' must not existing",
                actual: $"GridRowSnippetTextValues: {GridRowSnippetTextValues().AsFormattedJson()}");

            return this;
        }

        public bool CheckAllTextSnippetGrowsMatchSearchText(string searchText)
        {
            var searchWords = searchText.Split(" ").Where(word => !word.IsNullOrWhiteSpace()).ToList();

            return GridRowSnippetTextValues()
                .All(
                    rowSnippetTextValue => searchWords.Any(
                        searchWord => rowSnippetTextValue.Contains(searchWord, StringComparison.InvariantCultureIgnoreCase)));
        }

        public HomePage DoFillInAndSubmitSaveSnippetTextForm(TextSnippetData textSnippetData)
        {
            SaveSnippetFormSubmitBtn
                .WaitAndRetryUntil(
                    _ =>
                    {
                        SaveSnippetFormSnippetTextTxt.ReplaceTextAndEnter(textSnippetData.SnippetText);
                        SaveSnippetFormFullTextTxt.ReplaceTextAndEnter(textSnippetData.FullText);
                    },
                    until: _ => _.IsClickable(),
                    maxWaitSeconds: 3,
                    waitForMsg: "SaveSnippetFormSubmitBtn is clickable")
                .Click();

            WaitGlobalSpinnerStopped(
                DefaultMaxRequestWaitSeconds,
                "Wait for saving snippet text successfully");

            return this;
        }

        public List<string> GridRowSnippetTextValues()
        {
            return TextSnippetItemsGrid.Rows.Select(p => p.GetCell(colName: GripSnippetTextColName)!.RootElement!.Text).ToList();
        }
    }
}
