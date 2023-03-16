namespace PlatformExampleApp.Test.Shared.CommonUiComponents;

public class SpinnerUiComponent : UiComponent<SpinnerUiComponent>
{
    public SpinnerUiComponent(IWebDriver webDriver, IUiComponent? parent = null) : base(
        webDriver,
        directReferenceRootElement: null,
        parent)
    {
    }

    public override string RootElementClassSelector => ".platform-mat-mdc-spinner";
}
