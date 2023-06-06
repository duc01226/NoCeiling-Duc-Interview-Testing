using Easy.Platform.AutomationTest.Extensions;
using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.UiComponents;

public interface IUiComponent
{
    /// <summary>
    /// Given direct reference to direct parent component of the current component
    /// </summary>
    public IUiComponent? Parent { get; set; }

    /// <summary>
    /// This is used for the type of component selector. Usually is tagName, componentName, BEM block className
    /// <br />
    /// Example: button, .spinner, .panel, .grid
    /// </summary>
    public string? RootElementClassSelector { get; }

    /// <summary>
    /// This is optional, used to identity the UI component in other UI/Page component
    /// <br />
    /// Example: .app__global-spinner, .text-snippet-detail__main-form [name=\"snippetText\"]
    /// </summary>
    public string? IdentifierSelector { get; set; }

    public IWebDriver WebDriver { get; set; }

    /// <summary>
    /// Combine the <see cref="IdentifierSelector" /> and <see cref="RootElementClassSelector" /> to return a unique component selector of current instance on page
    /// </summary>
    public string? FullPathRootElementSelector { get; }

    /// <summary>
    /// Given directly element reference as root element
    /// </summary>
    public Func<IWebElement>? DirectReferenceRootElement { get; set; }

    /// <summary>
    /// Find and Get RootElement from <see cref="FullPathRootElementSelector" /> OR from <see cref="DirectReferenceRootElement" />
    /// </summary>
    public IWebElement? RootElement { get; }

    /// <summary>
    /// Resilient try get RootElement until it's available
    /// </summary>
    public IWebElement? TryRootElement { get; }

    public string Text { get; }

    public string GetAttribute(string attributeName);

    public bool IsClickable();
    public bool IsDisplayed();
    public IUiComponent WaitUntilClickable(double maxWaitSeconds, string? waitForMsg = null);
    public IUiComponent Clear(string? childElementSelector = null);
    public IUiComponent Click(string? childElementSelector = null);
    public IUiComponent SendKeys(string text, string? childElementSelector = null);
    public IUiComponent SendKeysAndFocusOut(string text, string? childElementSelector = null);
    public IUiComponent Submit(string? childElementSelector = null);
    public IUiComponent FocusOut(string? childElementSelector = null);
    public IUiComponent ReplaceTextValue(string text, string? childElementSelector = null);
    public IUiComponent ReplaceTextValueAndEnter(string text, string? childElementSelector = null);

    public IWebElement? FindChildOrRootElement(string? childElementSelector);

    public List<IWebElement> FindChildElements(string childElementSelector);

    public static IWebElement? FindRootElementBySelector(IUiComponent component)
    {
        return component.FullPathRootElementSelector
            .PipeIfNotNullOrDefault(thenPipe: selector => component.WebDriver.TryFindElement(cssSelector: selector!));
    }

    public static string? GetFullPathInPageElementSelector(IUiComponent component, IUiComponent? parent = null)
    {
        var identifierSelector = GetIdentifierSelector(component);

        var concatResult = parent == null
            ? identifierSelector
            : $"{parent.FullPathRootElementSelector ?? ""} {identifierSelector ?? ""}".Trim();

        return concatResult.IsNullOrWhiteSpace() ? null : concatResult;
    }

    public static string? GetIdentifierSelector(IUiComponent component)
    {
        return component.DirectReferenceRootElement != null
            ? component.DirectReferenceRootElement().ElementClassSelector()
            : $"{component.IdentifierSelector ?? ""}{component.RootElementClassSelector ?? ""}".Trim();
    }

    public static IWebElement? FindChildOrRootElement(IUiComponent component, string? childElementSelector)
    {
        return childElementSelector
            .PipeIfNotNull(
                thenPipe: childElementSelector => component.TryRootElement?.FindElement(by: By.CssSelector(childElementSelector)),
                component.TryRootElement);
    }

    public static List<IWebElement> FindChildElements(IUiComponent component, string childElementSelector)
    {
        return component.TryRootElement?.FindElements(by: By.CssSelector(childElementSelector)).ToList() ?? new List<IWebElement>();
    }
}

public interface IUiComponent<out TComponent> : IUiComponent
    where TComponent : IUiComponent<TComponent>
{
    public new TComponent Clear(string? childElementSelector = null);
    public new TComponent Click(string? childElementSelector = null);
    public new TComponent SendKeys(string text, string? childElementSelector = null);
    public new TComponent SendKeysAndFocusOut(string text, string? childElementSelector = null);
    public new TComponent Submit(string? childElementSelector = null);
    public new TComponent FocusOut(string? childElementSelector = null);
    public new TComponent WaitUntilClickable(double maxWaitSeconds, string? waitForMsg = null);
}

public abstract class UiComponent<TComponent> : IUiComponent<TComponent>
    where TComponent : UiComponent<TComponent>
{
    public const double DefaultMinimumDelayWaitSeconds = 0.5;
    public const int DefaultGetElementRetry = 2;

    public UiComponent(IWebDriver webDriver, Func<IWebElement>? directReferenceRootElement, IUiComponent? parent = null)
    {
        WebDriver = webDriver;
        DirectReferenceRootElement = directReferenceRootElement;
        Parent = parent;
    }

    public UiComponent(
        IWebDriver webDriver,
        string rootElementSelector,
        IUiComponent? parent = null) : this(webDriver, directReferenceRootElement: null, parent)
    {
        WebDriver = webDriver;
        RootElementClassSelector = rootElementSelector;
        Parent = parent;
    }

    public virtual string? RootElementClassSelector { get; }
    public string? IdentifierSelector { get; set; }
    public IWebDriver WebDriver { get; set; }
    public IUiComponent? Parent { get; set; }
    public Func<IWebElement>? DirectReferenceRootElement { get; set; }

    /// <summary>
    /// Get Component RootElement. Retry in case of the element get "stale element reference" exception.
    /// </summary>
    public IWebElement? RootElement
    {
        get
        {
            try
            {
                // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
                return Util.TaskRunner.WaitRetryThrowFinalException<IWebElement?, StaleElementReferenceException>(
                    executeFunc: () => DirectReferenceRootElement?.Invoke() ?? IUiComponent.FindRootElementBySelector(component: this),
                    retryCount: DefaultGetElementRetry);
            }
            catch (StaleElementReferenceException)
            {
                return null;
            }
        }
    }

    // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
    public IWebElement? TryRootElement =>
        Util.TaskRunner.WaitRetryThrowFinalException(
            () =>
            {
                if (RootElement == null) throw new Exception($"'WaitFor {FullPathRootElementSelector}' to be available");
                return RootElement;
            },
            retryCount: DefaultGetElementRetry);

    // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
    public virtual string Text =>
        Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () => RootElement?.Text ?? "",
            retryCount: DefaultGetElementRetry);

    // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
    public virtual string GetAttribute(string attributeName)
    {
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () => RootElement?.GetAttribute(attributeName) ?? "",
            retryCount: DefaultGetElementRetry);
    }

    // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
    public bool IsClickable()
    {
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () => RootElement?.IsClickable() == true,
            retryCount: DefaultGetElementRetry);
    }

    // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
    public bool IsDisplayed()
    {
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () => RootElement?.Displayed == true,
            retryCount: DefaultGetElementRetry);
    }

    public TComponent WaitUntilClickable(double maxWaitSeconds, string? waitForMsg = null)
    {
        return (TComponent)this.WaitUntil(condition: _ => _.IsClickable(), maxWaitSeconds, waitForMsg: waitForMsg);
    }

    IUiComponent IUiComponent.WaitUntilClickable(double maxWaitSeconds, string? waitForMsg)
    {
        return WaitUntilClickable(maxWaitSeconds, waitForMsg);
    }

    IUiComponent IUiComponent.Clear(string? childElementSelector)
    {
        return Clear(childElementSelector);
    }

    IUiComponent IUiComponent.Click(string? childElementSelector)
    {
        return Click(childElementSelector);
    }

    IUiComponent IUiComponent.SendKeys(string text, string? childElementSelector)
    {
        return SendKeys(text, childElementSelector);
    }

    IUiComponent IUiComponent.SendKeysAndFocusOut(string text, string? childElementSelector)
    {
        return SendKeysAndFocusOut(text, childElementSelector);
    }

    IUiComponent IUiComponent.Submit(string? childElementSelector)
    {
        return Submit(childElementSelector);
    }

    IUiComponent IUiComponent.FocusOut(string? childElementSelector)
    {
        return FocusOut(childElementSelector);
    }

    IUiComponent IUiComponent.ReplaceTextValue(string text, string? childElementSelector)
    {
        return ReplaceTextValue(text, childElementSelector);
    }

    IUiComponent IUiComponent.ReplaceTextValueAndEnter(string text, string? childElementSelector)
    {
        return ReplaceTextValueAndEnter(text, childElementSelector);
    }

    public TComponent Clear(string? childElementSelector = null)
    {
        FindChildOrRootElement(childElementSelector)!.Clear();
        HumanDelay();

        return (TComponent)this;
    }

    public TComponent Click(string? childElementSelector = null)
    {
        // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () =>
            {
                FindChildOrRootElement(childElementSelector)!.Click();
                HumanDelay();

                return (TComponent)this;
            },
            retryCount: DefaultGetElementRetry);
    }

    public TComponent SendKeys(string text, string? childElementSelector = null)
    {
        // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () =>
            {
                var element = FindChildOrRootElement(childElementSelector);

                element!.SendKeys(text);
                HumanDelay();

                return (TComponent)this;
            },
            retryCount: DefaultGetElementRetry);
    }

    public TComponent SendKeysAndFocusOut(string text, string? childElementSelector = null)
    {
        // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () =>
            {
                var element = FindChildOrRootElement(childElementSelector);

                element!.SendKeys(text);
                element.FocusOut(WebDriver);
                HumanDelay();

                return (TComponent)this;
            },
            retryCount: DefaultGetElementRetry);
    }

    public TComponent FocusOut(string? childElementSelector = null)
    {
        // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () =>
            {
                FindChildOrRootElement(childElementSelector)!.FocusOut(WebDriver);
                HumanDelay();

                return (TComponent)this;
            },
            retryCount: DefaultGetElementRetry);
    }

    public TComponent Submit(string? childElementSelector = null)
    {
        // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () =>
            {
                var element = FindChildOrRootElement(childElementSelector);
                element!.Submit();
                HumanDelay();

                return (TComponent)this;
            },
            retryCount: DefaultGetElementRetry);
    }

    public string? FullPathRootElementSelector => IUiComponent.GetFullPathInPageElementSelector(component: this, Parent);

    public IWebElement? FindChildOrRootElement(string? childElementSelector)
    {
        return IUiComponent.FindChildOrRootElement(component: this, childElementSelector);
    }

    public List<IWebElement> FindChildElements(string childElementSelector)
    {
        return IUiComponent.FindChildElements(component: this, childElementSelector);
    }

    public bool IsSelected()
    {
        // Retry to enhance testing resilient, prevent sometime got errors like StaleElementReferenceException event after get element sucess
        return Util.TaskRunner.WaitRetryThrowFinalException(
            executeFunc: () => RootElement?.IsSelected() == true,
            retryCount: DefaultGetElementRetry);
    }

    public TComponent ReplaceTextValue(string text, string? childElementSelector = null)
    {
        return InternalReplaceTextValue(text, childElementSelector, enterBeforeFocusOut: false);
    }

    public TComponent ReplaceTextValueAndEnter(string text, string? childElementSelector = null)
    {
        return InternalReplaceTextValue(text, childElementSelector, enterBeforeFocusOut: true);
    }

    public TComponent WithIdentifierSelector(string appSearchInput)
    {
        return (TComponent)this.With(_ => _.IdentifierSelector = appSearchInput);
    }

    public TComponent HumanDelay(double waitSeconds = DefaultMinimumDelayWaitSeconds)
    {
        Util.TaskRunner.Wait(millisecondsToWait: (int)(waitSeconds * 1000));
        return (TComponent)this;
    }

    public string? Value()
    {
        return RootElement?.Value();
    }

    private TComponent InternalReplaceTextValue(string newTextValue, string? childElementSelector, bool enterBeforeFocusOut = false)
    {
        var element = FindChildOrRootElement(childElementSelector);

        if (element != null)
        {
            element.Value()?.ForEach(_ => element.SendKeys(Keys.Backspace));

            if (!element.Value().IsNullOrEmpty())
                element.Clear();

            element.SendKeys(newTextValue);

            if (enterBeforeFocusOut) element.SendKeys(Keys.Return);

            element.FocusOut(WebDriver);

            HumanDelay();
        }

        return (TComponent)this;
    }
}
