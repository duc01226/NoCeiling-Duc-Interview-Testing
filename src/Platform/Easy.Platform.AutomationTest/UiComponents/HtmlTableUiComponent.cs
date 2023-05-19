using Easy.Platform.AutomationTest.Extensions;
using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.UiComponents;

public class HtmlTableUiComponent : UiComponent<HtmlTableUiComponent>
{
    public HtmlTableUiComponent(
        IWebDriver webDriver,
        Func<IWebElement>? directReferenceRootElement,
        IUiComponent? parent = null,
        Func<IWebElement, string>? getHeaderName = null) : base(webDriver, directReferenceRootElement, parent)
    {
        if (getHeaderName != null) GetHeaderName = getHeaderName;
    }

    public HtmlTableUiComponent(
        IWebDriver webDriver,
        string rootElementSelector,
        IUiComponent? parent = null,
        Func<IWebElement, string>? getHeaderName = null) : base(webDriver, rootElementSelector, parent)
    {
        if (getHeaderName != null) GetHeaderName = getHeaderName;
    }

    /// <summary>
    /// GetHeaderName from Headers elements. Default get Element Text
    /// </summary>
    public Func<IWebElement, string>? GetHeaderName { get; set; }

    public IEnumerable<Row> Rows => ReadRows();
    public IEnumerable<IWebElement> Headers => ReadHeaders();

    public IEnumerable<Row> ReadRows()
    {
        var rows = RootElement!.TryFindElement(cssSelector: "tbody") != null
            ? RootElement!.FindElements(by: By.CssSelector(cssSelectorToFind: "tbody > tr")).ToList()
            : RootElement!.FindElements(by: By.XPath(xpathToFind: "./tr")).ToList();

        return rows
            .Select(selector: (rowElement, rowIndex) => new Row(WebDriver, rowIndex, ReadHeaders, directReferenceRootElement: () => rowElement, parent: this));
    }

    public IEnumerable<IWebElement> ReadHeaders()
    {
        return RootElement!.FindElements(by: By.TagName(tagNameToFind: "th"));
    }

    public Cell? GetCell(int rowIndex, int colIndex)
    {
        return Rows.ElementAtOrDefault(rowIndex)?.GetCell(colIndex);
    }

    public Cell? GetCell(int rowIndex, string colName)
    {
        return Rows.ElementAtOrDefault(rowIndex)?.GetCell(colName);
    }

    public Row? ClickOnRow(int rowIndex)
    {
        var rowToClick = Rows.ElementAtOrDefault(rowIndex);

        rowToClick?.Click();

        return rowToClick;
    }

    public class Cell : UiComponent<Cell>
    {
        public Cell(IWebDriver webDriver, Func<IWebElement>? directReferenceRootElement, IUiComponent? parent = null) : base(
            webDriver,
            directReferenceRootElement,
            parent)
        {
        }

        public int ColIndex { get; set; }
        public int RowIndex { get; set; }
        public string? ColName { get; set; }
        public string? Value { get; set; }
    }

    public class Row : UiComponent<Row>
    {
        public Row(
            IWebDriver webDriver,
            int rowIndex,
            Func<IEnumerable<IWebElement>> headers,
            Func<IWebElement>? directReferenceRootElement,
            IUiComponent? parent = null,
            Func<IWebElement, string>? getHeaderName = null) : base(webDriver, directReferenceRootElement, parent)
        {
            RowIndex = rowIndex;
            Headers = headers;
            GetHeaderName = getHeaderName ?? GetHeaderName;
        }

        public Row(
            IWebDriver webDriver,
            int rowIndex,
            Func<IEnumerable<IWebElement>> headers,
            string rootElementSelector,
            IUiComponent? parent = null,
            Func<IWebElement, string>? getHeaderName = null) : base(webDriver, rootElementSelector, parent)
        {
            RowIndex = rowIndex;
            Headers = headers;
            GetHeaderName = getHeaderName ?? GetHeaderName;
        }

        public IEnumerable<Cell> Cells => ReadCells(Headers);
        public int RowIndex { get; set; }
        public Func<IEnumerable<IWebElement>> Headers { get; }

        /// <summary>
        /// GetHeaderName from Headers elements. Default get Element Text
        /// </summary>
        public Func<IWebElement, string> GetHeaderName { get; set; } = headerElement => headerElement.Text;

        public IEnumerable<Cell> ReadCells(Func<IEnumerable<IWebElement>> headers)
        {
            try
            {
                var rowCells = RootElement!.FindElements(by: By.TagName(tagNameToFind: "td"));

                return rowCells
                    .Select((cellElement, cellIndex) => TryBuildCell(headers, cellElement, cellIndex))
                    .Where(p => p != null)
                    .As<IEnumerable<Cell>>();
            }
            // catch StaleElementReferenceException when if table has been updated and render again then just consider that row is empty no cell
            catch (StaleElementReferenceException)
            {
                return Enumerable.Empty<Cell>();
            }
        }

        protected Cell? TryBuildCell(Func<IEnumerable<IWebElement>> headers, IWebElement cellElement, int cellIndex)
        {
            try
            {
                return new Cell(WebDriver, directReferenceRootElement: () => cellElement, parent: this)
                {
                    ColIndex = cellIndex,
                    RowIndex = RowIndex,
                    ColName = headers().ElementAtOrDefault(cellIndex).PipeIfNotNull(thenPipe: p => GetHeaderName(arg: p!)),
                    Value = cellElement.Text
                };
            }
            // catch StaleElementReferenceException when if table has been updated and render again then just consider that row is empty no cell
            catch (StaleElementReferenceException)
            {
                return null;
            }
        }

        public Cell? GetCell(int colIndex)
        {
            return Cells.ElementAtOrDefault(colIndex);
        }

        public Cell? GetCell(string colName)
        {
            return Cells.FirstOrDefault(predicate: p => p.ColName == colName);
        }
    }
}
