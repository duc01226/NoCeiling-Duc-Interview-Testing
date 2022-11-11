using Easy.Platform.AutomationTest.Extensions;
using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.UiComponents;

public interface IUiComponent
{
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
    public string? FullPathRootElementSelector { get; }

    /// <summary>
    /// Find and Get RootElement from ComponentTypeSelector and IdentifierSelector
    /// </summary>
    public IWebElement? RootElementBySelector { get; }

    public Func<IWebElement>? FixedRootElement { get; set; }
    public IWebElement? RootElement { get; }
    public bool IsClickable();
    public IUiComponent WaitUntilClickable(double maxWaitSeconds, string? waitForMsg = null);
    public IUiComponent Clear(string? childElementSelector = null);
    public IUiComponent Click(string? childElementSelector = null);
    public IUiComponent SendKeys(string text, string? childElementSelector = null);
    public IUiComponent SendKeysAndFocusOut(string text, string? childElementSelector = null);
    public IUiComponent Submit(string? childElementSelector = null);
    public IUiComponent FocusOut(string? childElementSelector = null);
    public IUiComponent ReplaceText(string text, string? childElementSelector = null);
    public IUiComponent ReplaceTextAndEnter(string text, string? childElementSelector = null);

    public IWebElement? FindChildOrRootElement(string? childElementSelector);

    public List<IWebElement> FindChildElements(string childElementSelector);

    public static IWebElement? FindRootElementBySelector(IUiComponent component)
    {
        return component.FullPathRootElementSelector
            .PipeIfNotNullOrDefault(selector => component.WebDriver.TryFindElement(selector!));
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
        return component.FixedRootElement != null
            ? component.FixedRootElement().ElementClassSelector()
            : $"{component.IdentifierSelector ?? ""}{component.RootElementClassSelector ?? ""}".Trim();
    }

    public static IWebElement? FindChildOrRootElement(IUiComponent component, string? childElementSelector)
    {
        return childElementSelector
            .PipeIfNotNull(
                childElementSelector => component.RootElement?.FindElement(By.CssSelector(childElementSelector)),
                component.RootElement);
    }

    public static List<IWebElement> FindChildElements(IUiComponent component, string childElementSelector)
    {
        return component.RootElement?.FindElements(By.CssSelector(childElementSelector)).ToList() ?? new List<IWebElement>();
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
    public new TComponent ReplaceText(string text, string? childElementSelector = null);
    public new TComponent ReplaceTextAndEnter(string text, string? childElementSelector = null);
}

public abstract class UiComponent<TComponent> : IUiComponent<TComponent>
    where TComponent : UiComponent<TComponent>
{
    public UiComponent(IWebDriver webDriver, Func<IWebElement>? fixedRootElement, IUiComponent? parent = null)
    {
        WebDriver = webDriver;
        FixedRootElement = fixedRootElement;
        Parent = parent;
    }

    public UiComponent(
        IWebDriver webDriver,
        string rootElementSelector,
        IUiComponent? parent = null) : this(webDriver, fixedRootElement: null, parent)
    {
        WebDriver = webDriver;
        RootElementClassSelector = rootElementSelector;
        Parent = parent;
    }

    public virtual string? RootElementClassSelector { get; }
    public string? IdentifierSelector { get; set; }
    public IWebDriver WebDriver { get; set; }
    public IUiComponent? Parent { get; set; }

    public IWebElement? RootElementBySelector => IUiComponent.FindRootElementBySelector(this);
    public Func<IWebElement>? FixedRootElement { get; set; }

    /// <summary>
    /// Get Component RootElement. Retry in case of the element get "stale element reference" exception.
    /// </summary>
    public IWebElement? RootElement =>
        Util.TaskRunner.WaitRetryThrowFinalException<IWebElement?, StaleElementReferenceException>(
            () => FixedRootElement?.Invoke() ?? RootElementBySelector);

    public bool IsClickable()
    {
        return RootElement?.IsClickable() == true;
    }

    public TComponent WaitUntilClickable(double maxWaitSeconds, string? waitForMsg = null)
    {
        return (TComponent)this.WaitUntil(_ => _.IsClickable(), maxWaitSeconds, waitForMsg: waitForMsg);
    }

    public TComponent ReplaceText(string text, string? childElementSelector = null)
    {
        return InternalReplaceText(text, childElementSelector);
    }

    public TComponent ReplaceTextAndEnter(string text, string? childElementSelector = null)
    {
        return InternalReplaceText(text, childElementSelector, true);
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

    IUiComponent IUiComponent.ReplaceText(string text, string? childElementSelector)
    {
        return ReplaceText(text, childElementSelector);
    }

    IUiComponent IUiComponent.ReplaceTextAndEnter(string text, string? childElementSelector)
    {
        return ReplaceTextAndEnter(text, childElementSelector);
    }

    public TComponent Clear(string? childElementSelector = null)
    {
        FindChildOrRootElement(childElementSelector)!.Clear();
        HumanDelay();

        return (TComponent)this;
    }

    public TComponent Click(string? childElementSelector = null)
    {
        FindChildOrRootElement(childElementSelector)!.Click();
        HumanDelay();

        return (TComponent)this;
    }

    public TComponent SendKeys(string text, string? childElementSelector = null)
    {
        var element = FindChildOrRootElement(childElementSelector);

        element!.SendKeys(text);
        HumanDelay();

        return (TComponent)this;
    }

    public TComponent SendKeysAndFocusOut(string text, string? childElementSelector = null)
    {
        var element = FindChildOrRootElement(childElementSelector);

        element!.SendKeys(text);
        element.FocusOut(WebDriver);
        HumanDelay();

        return (TComponent)this;
    }

    public TComponent FocusOut(string? childElementSelector = null)
    {
        FindChildOrRootElement(childElementSelector)!.FocusOut(WebDriver);
        HumanDelay();

        return (TComponent)this;
    }

    public TComponent Submit(string? childElementSelector = null)
    {
        var element = FindChildOrRootElement(childElementSelector);
        element!.Submit();
        HumanDelay();

        return (TComponent)this;
    }

    public string? FullPathRootElementSelector => IUiComponent.GetFullPathInPageElementSelector(this, Parent);

    public IWebElement? FindChildOrRootElement(string? childElementSelector)
    {
        return IUiComponent.FindChildOrRootElement(this, childElementSelector);
    }

    public List<IWebElement> FindChildElements(string childElementSelector)
    {
        return IUiComponent.FindChildElements(this, childElementSelector);
    }

    public TComponent WithIdentifierSelector(string appSearchInput)
    {
        return (TComponent)this.With(_ => _.IdentifierSelector = appSearchInput);
    }

    public TComponent HumanDelay(double waitSeconds = Util.TaskRunner.DefaultMinimumDelayWaitSeconds)
    {
        Util.TaskRunner.Wait((int)(waitSeconds * 1000));
        return (TComponent)this;
    }

    private TComponent InternalReplaceText(string text, string? childElementSelector, bool enterBeforeFocusOut = false)
    {
        var element = FindChildOrRootElement(childElementSelector);

        element!.Clear();
        element.SendKeys(text);
        if (enterBeforeFocusOut) element.SendKeys(Keys.Return);
        element.FocusOut(WebDriver);
        HumanDelay();

        return (TComponent)this;
    }
}
