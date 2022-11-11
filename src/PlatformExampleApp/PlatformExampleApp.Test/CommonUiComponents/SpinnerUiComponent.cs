namespace PlatformExampleApp.Test.CommonUiComponents;

public class SpinnerUiComponent : UiComponent<SpinnerUiComponent>
{
    public SpinnerUiComponent(IWebDriver webDriver, Func<IWebElement>? fixedRootElement, IUiComponent? parent = null) : base(webDriver, fixedRootElement, parent)
    {
    }

    public override string RootElementClassSelector => ".platform-mat-spinner";
}
