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
        public override string ErrorElementCssSelector => ".mat-error";
        public override IWebElement? GlobalSpinnerElement => GlobalSpinner.RootElement;

        public override TResult WaitUntilAssertSuccess<TResult>(
            Func<TPage, TResult> waitForSuccess,
            double maxWaitSeconds = DefaultMaxRequestWaitSeconds)
        {
            return base.WaitUntilAssertSuccess(waitForSuccess, maxWaitSeconds);
        }

        public override TResult WaitUntilAssertSuccess<TResult>(
            Func<TPage, TResult> waitForSuccess,
            Action<TPage> stopIfFail,
            double maxWaitSeconds = DefaultMaxRequestWaitSeconds)
        {
            return base.WaitUntilAssertSuccess(waitForSuccess, stopIfFail, maxWaitSeconds);
        }

        public override TResult WaitUntilAssertSuccess<TResult, TStopIfFailResult>(
            Func<TPage, TResult> waitForSuccess,
            Func<TPage, TStopIfFailResult> stopIfFail,
            double maxWaitSeconds = DefaultMaxRequestWaitSeconds)
        {
            return base.WaitUntilAssertSuccess(waitForSuccess, stopIfFail, maxWaitSeconds);
        }

        public override TPage WaitGlobalSpinnerStopped(
            int maxWaitForLoadingDataSeconds = DefaultMaxRequestWaitSeconds,
            string waitForMsg = "Page Global Spinner is stopped")
        {
            return base.WaitGlobalSpinnerStopped(maxWaitForLoadingDataSeconds, waitForMsg);
        }
    }
}
