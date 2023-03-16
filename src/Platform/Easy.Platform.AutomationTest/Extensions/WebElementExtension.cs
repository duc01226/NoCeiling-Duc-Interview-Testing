using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Easy.Platform.AutomationTest.Extensions;

public static class WebElementExtension
{
    public static IWebElement FindElement(this IWebElement webElement, string cssSelector)
    {
        return webElement.FindElement(by: By.CssSelector(cssSelector));
    }

    public static IWebElement? TryFindElement(this IWebElement webElement, string cssSelector)
    {
        return Util.TaskRunner.CatchException(func: () => webElement.FindElement(by: By.CssSelector(cssSelector)), fallbackValue: null);
    }

    public static List<IWebElement> FindElements(this IWebElement webElement, string cssSelector)
    {
        return webElement.FindElements(by: By.CssSelector(cssSelector)).ToList();
    }

    public static bool IsClickable(this IWebElement? element)
    {
        try
        {
            return element?.Pipe(fn: _ => _ is { Displayed: true, Enabled: true }) ?? false;
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    public static IWebElement? FocusOut(
        this IWebElement? element,
        IWebDriver webDriver,
        params IWebElement[] focusOutToOtherElements)
    {
        element?.WaitAndRetryUntil(
            action: _ =>
            {
                webDriver.TryFindElement(cssSelector: "body")?.Click();

                Util.ListBuilder.New("body div:visible", "header", "footer", "h1", "h2", "h3")
                    .Select<string, Action>(selector: elementSelector => () => webDriver.TryFindElement(elementSelector)?.Click())
                    .Concat(second: focusOutToOtherElements.Select<IWebElement, Action>(selector: element => () => element.Click()))
                    .ForEach(
                        action: action =>
                        {
                            if (element.ToStaleElementWrapper().Get(_ => _.Selected)) action();
                        });
            },
            until: _ => !element.ToStaleElementWrapper().Get(_ => _.Selected),
            maxWaitSeconds: 2);

        return element;
    }

    public static StaleWebElementWrapper ToStaleElementWrapper(this IWebElement element)
    {
        return new StaleWebElementWrapper(element);
    }

    public static string? Value(this IWebElement? element)
    {
        return element?.GetAttribute(attributeName: "value");
    }

    /// <summary>
    /// Selector to identify the CLASS (in OOP) type of the element represent a component. Example: button.btn.btn-danger
    /// </summary>
    public static string? ElementClassSelector(this IWebElement? element)
    {
        return element?.PipeIfNotNull(
            thenPipe: _ => element.TagName +
                           element.GetCssValue(propertyName: "class").Split(separator: " ").Select(selector: className => $".{className}").JoinToString());
    }

    public static IWebElement SelectDropdownByText(this IWebElement element, string text)
    {
        var select = new SelectElement(element);

        select.SelectByText(text);

        return element;
    }

    public static IWebElement SelectDropdownByValue(this IWebElement element, string value)
    {
        var select = new SelectElement(element);

        select.SelectByValue(value);

        return element;
    }

    public static IWebElement SelectDropdownByText(this IWebElement element, int index)
    {
        var select = new SelectElement(element);

        select.SelectByIndex(index);

        return element;
    }

    public static string? SelectedDropdownValue(this IWebElement element)
    {
        return new SelectElement(element).SelectedOption.Text;
    }

    public class StaleWebElementWrapper
    {
        private readonly IWebElement element;

        public StaleWebElementWrapper(IWebElement element)
        {
            this.element = element;
        }

        public T Get<T>(Func<IWebElement, T> getFn)
        {
            return Util.TaskRunner.CatchException<StaleElementReferenceException, T>(func: () => getFn(element));
        }
    }
}
