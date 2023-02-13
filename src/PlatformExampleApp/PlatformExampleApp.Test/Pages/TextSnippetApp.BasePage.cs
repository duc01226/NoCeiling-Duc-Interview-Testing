using PlatformExampleApp.Test.CommonUiComponents;

namespace PlatformExampleApp.Test.Apps.TextSnippet.Pages;

public static partial class TextSnippetApp
{
    public static class Const
    {
        public const string AppName = "TextSnippetApp";
    }

    public abstract class BasePage<TPage> : Page<TPage, TestSettings>
        where TPage : BasePage<TPage>
    {
        public const int DefaultMaxRequestWaitSeconds = 5;

        public BasePage(IWebDriver webDriver, TestSettings settings) : base(webDriver, settings)
        {
            GlobalSpinner = new SpinnerUiComponent(webDriver, null, this);
        }

        public SpinnerUiComponent GlobalSpinner { get; set; }

        public override string AppName => Const.AppName;
        public override string ErrorElementCssSelector => ".mat-mdc-error";
        public override IWebElement? GlobalSpinnerElement => GlobalSpinner.RootElement;

        protected override int DefaultWaitUntilMaxSeconds => DefaultMaxRequestWaitSeconds;
    }
}
