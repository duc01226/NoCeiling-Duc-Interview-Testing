namespace PlatformExampleApp.Test.CommonUiComponents;

public class SpinnerUiComponent : UiComponent<SpinnerUiComponent>
{
    public SpinnerUiComponent(IWebDriver webDriver, Func<IWebElement>? directReferenceRootElement, IUiComponent? parent = null) : base(
        webDriver,
        directReferenceRootElement,
        parent)
    {
    }

    public override string RootElementClassSelector => ".platform-mat-mdc-spinner";
}
