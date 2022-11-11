namespace PlatformExampleApp.Test.CommonUiComponents;

public class FormFieldUiComponent : UiComponent<FormFieldUiComponent>
{
    public FormFieldUiComponent(IWebDriver webDriver, Func<IWebElement>? fixedRootElement, IUiComponent? parent = null) : base(webDriver, fixedRootElement, parent)
    {
    }

    public override string RootElementClassSelector => ".mat-form-field";
    public IWebElement? InputElement => FindChildOrRootElement(".mat-input-element");
    public string Value => InputElement?.Value() ?? "";

    public FormFieldUiComponent SendKeysAndFocusOut(string text)
    {
        return SendKeysAndFocusOut(text, ".mat-input-element");
    }

    public FormFieldUiComponent Clear()
    {
        return Clear(".mat-input-element");
    }

    public FormFieldUiComponent ReplaceTextAndEnter(string text)
    {
        return Clear().SendKeysAndFocusOut(text);
    }
}
