using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.UiComponents;

public class GeneralUiComponent : UiComponent<GeneralUiComponent>
{
    public GeneralUiComponent(IWebDriver webDriver, Func<IWebElement>? directReferenceRootElement, IUiComponent? parent = null) : base(
        webDriver,
        directReferenceRootElement,
        parent)
    {
    }

    public GeneralUiComponent(IWebDriver webDriver, string rootElementClassSelector, IUiComponent? parent = null) : base(webDriver, rootElementClassSelector, parent)
    {
    }
}
