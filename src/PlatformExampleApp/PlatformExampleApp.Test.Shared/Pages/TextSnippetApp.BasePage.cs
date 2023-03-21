using PlatformExampleApp.Test.Shared.CommonUiComponents;

namespace PlatformExampleApp.Test.Shared.Pages;

public static partial class TextSnippetApp
{
    public static class Const
    {
        public const string AppName = "TextSnippetApp";
        public const int DefaultMaxWaitSeconds = 5;
    }

    public abstract class BasePage<TPage> : Page<TPage, AutomationTestSettings>
        where TPage : BasePage<TPage>
    {
        public BasePage(IWebDriver webDriver, AutomationTestSettings settings) : base(webDriver, settings)
        {
        }

        public override string AppName => Const.AppName;
        public override string? GeneralErrorElementsCssSelector => ".app__errors-content";
        public override string? FormValidationErrorElementsCssSelector => ".mat-mdc-form-field-error";
        public override IWebElement? GlobalSpinnerElement => GlobalSpinner.RootElement;

        public override int DefaultMaxWaitSeconds => Const.DefaultMaxWaitSeconds;

        public SpinnerUiComponent GlobalSpinner => new(WebDriver, parent: this);
    }
}
