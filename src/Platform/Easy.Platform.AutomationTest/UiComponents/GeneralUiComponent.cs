using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.UiComponents;

public class GeneralUiComponent : UiComponent<GeneralUiComponent>
{
    public GeneralUiComponent(IWebDriver webDriver, Func<IWebElement>? fixedRootElement, IUiComponent? parent = null) : base(webDriver, fixedRootElement, parent)
    {
    }

    public GeneralUiComponent(IWebDriver webDriver, string rootElementSelector, IUiComponent? parent = null) : base(webDriver, rootElementSelector, parent)
    {
    }
}
