using Easy.Platform.AutomationTest.Extensions;
using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.UiComponents;

public class HtmlTableUiComponent : UiComponent<HtmlTableUiComponent>
{
    public HtmlTableUiComponent(
        IWebDriver webDriver,
        Func<IWebElement>? fixedRootElement,
        IUiComponent? parent = null) : base(webDriver, fixedRootElement, parent)
    {
        Columns = ReadColumns();
        Rows = ReadRows();
    }

    public HtmlTableUiComponent(
        IWebDriver webDriver,
        string rootElementSelector,
        IUiComponent? parent = null) : base(webDriver, rootElementSelector, parent)
    {
        Columns = ReadColumns();
        Rows = ReadRows();
    }

    public List<Row> Rows { get; set; } = new();
    public List<IWebElement> Columns { get; set; }

    public List<Row> ReadRows()
    {
        var rows = RootElement!.TryFindElement("tbody") != null
            ? RootElement!.FindElements(By.CssSelector("tbody > tr")).ToList()
            : RootElement!.FindElements(By.XPath("./tr")).ToList();

        return rows
            .Select((rowElement, rowIndex) => new Row(WebDriver, rowIndex, Columns, fixedRootElement: () => rowElement, this))
            .ToList();
    }

    public List<IWebElement> ReadColumns()
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
        public Cell(IWebDriver webDriver, Func<IWebElement>? fixedRootElement, IUiComponent? parent = null) : base(webDriver, fixedRootElement, parent)
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
            Func<IWebElement>? fixedRootElement,
            IUiComponent? parent = null) : base(webDriver, fixedRootElement, parent)
        {
            RowIndex = rowIndex;
            Cells = ReadCells(columns);
        }

        public Row(
            IWebDriver webDriver,
            int rowIndex,
            List<IWebElement> columns,
            string rootElementSelector,
            IUiComponent? parent = null) : base(webDriver, rootElementSelector, parent)
        {
            RowIndex = rowIndex;
            Cells = ReadCells(columns);
        }

        public List<Cell> Cells { get; set; }
        public int RowIndex { get; set; }

        public List<Cell> ReadCells(List<IWebElement> columns)
        {
            var rowCells = RootElement!.FindElements(By.TagName("td"));

            return rowCells
                .Select(
                    (cellElement, cellIndex) => new Cell(WebDriver, () => cellElement, this)
                    {
                        ColIndex = cellIndex,
                        RowIndex = RowIndex,
                        ColName = columns.ElementAtOrDefault(cellIndex)?.Text,
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
