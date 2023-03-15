using PlatformExampleApp.Test.Shared.CommonUiComponents;

namespace PlatformExampleApp.Test.Shared.Pages;

public static partial class TextSnippetApp
{
    public static class Const
    {
        public const string AppName = "TextSnippetApp";
    }

    public abstract class BasePage<TPage> : Page<TPage, AutomationTestSettings>
        where TPage : BasePage<TPage>
    {
        public const int DefaultMaxRequestWaitSeconds = 5;

        public BasePage(IWebDriver webDriver, AutomationTestSettings settings) : base(webDriver, settings)
        {
            GlobalSpinner = new SpinnerUiComponent(webDriver, directReferenceRootElement: null, parent: this);
        }

        public override string AppName => Const.AppName;
        public override string ErrorElementCssSelector => ".mat-mdc-error";
        public override IWebElement? GlobalSpinnerElement => GlobalSpinner.RootElement;

        protected override int DefaultWaitUntilMaxSeconds => DefaultMaxRequestWaitSeconds;

        public SpinnerUiComponent GlobalSpinner { get; set; }
    }
}
