using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace Easy.Platform.AutomationTest.Extensions;

public static class WebElementExtension
{
    public const int DefaultShortWaitUiUpdateSeconds = 5;

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

    public static bool IsSelected(this IWebElement? element)
    {
        try
        {
            return element?.Pipe(fn: _ => _ is { Selected: true }) ?? false;
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    public static IWebElement? FocusOut(this IWebElement? element, IWebDriver webDriver, params IWebElement[] additionalFocusToOtherElements)
    {
        element?.WaitRetryDoUntil(
            action: element =>
            {
                webDriver.FindElement(cssSelector: "body").SendKeys(Keys.Escape);

                if (element.IsFocused(webDriver))
                    TryActionToFocusOutToOtherElement(
                        element,
                        webDriver,
                        additionalFocusToOtherElements,
                        otherElement => new Actions(webDriver).SendKeys(Keys.Escape).Perform()
                    );
            },
            until: _ => !element.IsFocused(webDriver),
            maxWaitSeconds: DefaultShortWaitUiUpdateSeconds
        );

        return element;
    }

    public static void TryActionToFocusOutToOtherElement(
        IWebElement targetElement,
        IWebDriver webDriver,
        IWebElement[] additionalFocusToOtherElements,
        Action<IWebElement> otherElementAction
    )
    {
        webDriver.TryFindElement(cssSelector: "body")?.PipeAction(otherElementAction);

        if (IsFocused(targetElement, webDriver))
        {
            var otherElementActions = Util.ListBuilder
                .New("body", "body > *", "p", "header", "footer", "h1", "h2", "h3")
                .SelectMany(webDriver.FindElements)
                .Where(p => p.IsClickable())
                .Select<IWebElement, Action>(otherElement => () => otherElementAction(otherElement))
                .Concat(additionalFocusToOtherElements.Select<IWebElement, Action>(element => () => element.PipeAction(otherElementAction)))
                .Where(otherElement => !otherElement.Equals(targetElement));

            foreach (var action in otherElementActions)
                if (IsFocused(targetElement, webDriver))
                    action();
                else
                    return;
        }
    }

    public static bool IsFocused(this IWebElement element, IWebDriver webDriver)
    {
        return webDriver.SwitchTo().ActiveElement().Equals(element) || element.ToStaleElementWrapper().Get(p => p.Selected);
    }

    /// <summary>
    /// To StaleWebElementWrapper help to get any thing from an element and return default value if the element get staled
    /// </summary>
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
            thenPipe: _ =>
                element.TagName + element.GetCssValue(propertyName: "class").Split(separator: " ").Select(selector: className => $".{className}").JoinToString()
        );
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

    public static bool ContainsText(this IWebElement element, string text)
    {
        try
        {
            return element.Text.Contains(text);
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    public static bool ContainsValue(this IWebElement element, string value)
    {
        try
        {
            var attribute = element.GetAttribute("value");
            return attribute != null && attribute.Contains(value);
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    /// <summary>
    /// StaleWebElementWrapper help to get any thing from an element and return default value if the element get staled
    /// </summary>
    public class StaleWebElementWrapper
    {
        private readonly IWebElement element;

        public StaleWebElementWrapper(IWebElement element)
        {
            this.element = element;
        }

        /// <summary>
        /// Get any thing from an element and return default value if the element get staled
        /// </summary>
        public T? Get<T>(Func<IWebElement, T> getFn, T? defaultValue = default)
        {
            try
            {
                return getFn(element);
            }
            catch (StaleElementReferenceException)
            {
                if (defaultValue != null) return defaultValue;

                return default;
            }
        }

        public StaleWebElementWrapper Execute(Action<IWebElement> getFn)
        {
            Util.TaskRunner.CatchException<StaleElementReferenceException, object>(func: () => getFn.ToFunc()(element));

            return this;
        }
    }
}
