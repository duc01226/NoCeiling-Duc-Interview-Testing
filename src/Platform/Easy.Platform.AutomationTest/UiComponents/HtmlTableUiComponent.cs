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
        Headers = ReadHeaders();
        Rows = ReadRows();
        if (getHeaderName != null) GetHeaderName = getHeaderName;
    }

    public HtmlTableUiComponent(
        IWebDriver webDriver,
        string rootElementClassSelector,
        IUiComponent? parent = null,
        Func<IWebElement, string>? getHeaderName = null) : base(webDriver, rootElementClassSelector, parent)
    {
        Headers = ReadHeaders();
        Rows = ReadRows();
        if (getHeaderName != null) GetHeaderName = getHeaderName;
    }

    /// <summary>
    /// GetHeaderName from Headers elements. Default get Element Text
    /// </summary>
    public Func<IWebElement, string>? GetHeaderName { get; set; }

    public List<Row> Rows { get; set; }
    public List<IWebElement> Headers { get; set; }

    public List<Row> ReadRows()
    {
        var rows = RootElement!.TryFindElement("tbody") != null
            ? RootElement!.FindElements(By.CssSelector("tbody > tr")).ToList()
            : RootElement!.FindElements(By.XPath("./tr")).ToList();

        return rows
            .Select((rowElement, rowIndex) => new Row(WebDriver, rowIndex, Headers, directReferenceRootElement: () => rowElement, this))
            .ToList();
    }

    public List<IWebElement> ReadHeaders()
    {
        return RootElement!.FindElements(By.TagName("th")).ToList();
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
            List<IWebElement> columns,
            Func<IWebElement>? directReferenceRootElement,
            IUiComponent? parent = null,
            Func<IWebElement, string>? getHeaderName = null) : base(webDriver, directReferenceRootElement, parent)
        {
            RowIndex = rowIndex;
            Cells = ReadCells(columns);
            GetHeaderName = getHeaderName ?? GetHeaderName;
        }

        public Row(
            IWebDriver webDriver,
            int rowIndex,
            List<IWebElement> columns,
            string rootElementClassSelector,
            IUiComponent? parent = null,
            Func<IWebElement, string>? getHeaderName = null) : base(webDriver, rootElementClassSelector, parent)
        {
            RowIndex = rowIndex;
            Cells = ReadCells(columns);
            GetHeaderName = getHeaderName ?? GetHeaderName;
        }

        public List<Cell> Cells { get; set; }
        public int RowIndex { get; set; }

        /// <summary>
        /// GetHeaderName from Headers elements. Default get Element Text
        /// </summary>
        public Func<IWebElement, string> GetHeaderName { get; set; } = headerElement => headerElement.Text;

        public List<Cell> ReadCells(List<IWebElement> columns)
        {
            var rowCells = RootElement!.FindElements(By.TagName("td"));

            return rowCells
                .Select(
                    (cellElement, cellIndex) => new Cell(WebDriver, () => cellElement, this)
                    {
                        ColIndex = cellIndex,
                        RowIndex = RowIndex,
                        ColName = columns.ElementAtOrDefault(cellIndex).PipeIfNotNull(p => GetHeaderName(p!)),
                        Value = cellElement.Text
                    })
                .ToList();
        }

        public Cell? GetCell(int colIndex)
        {
            return Cells.ElementAtOrDefault(colIndex);
        }

        public Cell? GetCell(string colName)
        {
            return Cells.FirstOrDefault(p => p.ColName == colName);
        }
    }
}
