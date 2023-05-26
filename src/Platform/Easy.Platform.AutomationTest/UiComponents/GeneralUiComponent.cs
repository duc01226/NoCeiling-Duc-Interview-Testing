using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.UiComponents;

public sealed class GeneralUiComponent : UiComponent<GeneralUiComponent>
{
    public GeneralUiComponent(IWebDriver webDriver, Func<IWebElement>? directReferenceRootElement, IUiComponent? parent = null) : base(
        webDriver,
        directReferenceRootElement,
        parent)
    {
    }

    public GeneralUiComponent(IWebDriver webDriver, string rootElementSelector, IUiComponent? parent = null) : base(webDriver, rootElementSelector, parent)
    {
    }
}
