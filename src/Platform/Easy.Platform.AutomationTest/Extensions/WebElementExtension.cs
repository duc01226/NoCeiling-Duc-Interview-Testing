using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Easy.Platform.AutomationTest.Extensions;

public static class WebElementExtension
{
    public static IWebElement FindElement(this IWebElement webElement, string cssSelector)
    {
        return webElement.FindElement(By.CssSelector(cssSelector));
    }

    public static IWebElement? TryFindElement(this IWebElement webElement, string cssSelector)
    {
        return Util.TaskRunner.CatchException(() => webElement.FindElement(By.CssSelector(cssSelector)), fallbackValue: null);
    }

    public static List<IWebElement> FindElements(this IWebElement webElement, string cssSelector)
    {
        return webElement.FindElements(By.CssSelector(cssSelector)).ToList();
    }

    public static bool? IsClickable(this IWebElement? element)
    {
        try
        {
            return element?.Pipe(_ => _ is { Displayed: true, Enabled: true });
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
        if (element != null)
            element.WaitAndRetryUntil(
                action: _ =>
                {
                    webDriver.TryFindElement("body")?.Click();

                    Util.ListBuilder.New("body div:visible", "header", "footer", "h1", "h2", "h3")
                        .Select<string, Action>(elementSelector => () => webDriver.TryFindElement(elementSelector)?.Click())
                        .Concat(focusOutToOtherElements.Select<IWebElement, Action>(element => () => element.Click()))
                        .ForEach(
                            action =>
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
        return element?.GetAttribute("value");
    }

    /// <summary>
    /// Selector to identify the CLASS (in OOP) type of the element represent a component. Example: button.btn.btn-danger
    /// </summary>
    public static string? ElementClassSelector(this IWebElement? element)
    {
        return element?.PipeIfNotNull(_ => element.TagName + element.GetCssValue("class").Split(" ").Select(className => $".{className}").JoinToString());
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
            return Util.TaskRunner.CatchException<StaleElementReferenceException, T>(() => getFn(element));
        }
    }
}
