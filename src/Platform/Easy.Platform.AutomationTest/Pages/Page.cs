using Easy.Platform.AutomationTest.Extensions;
using Easy.Platform.AutomationTest.Helpers;
using Easy.Platform.AutomationTest.TestFrameworks.Xunit;
using Easy.Platform.AutomationTest.UiComponents;
using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.Pages;

public interface IPage<TPage, out TSettings> : IUiComponent where TPage : IPage<TPage, TSettings> where TSettings : AutomationTestSettings
{
    public string AppName { get; }
    public string Origin { get; }
    public string Path { get; }

    /// <summary>
    /// The Base default url of a page with out query
    /// </summary>
    public string BaseUrl { get; }

    public Dictionary<string, string?>? QueryParams { get; set; }

    public string QueryParamsUrlPart { get; }

    public Uri FullUrl { get; }

    public string Title { get; }

    public IWebElement? GlobalSpinnerElement { get; }

    public TSettings Settings();

    public PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched();

    public PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched();

    public PlatformValidationResult<TPage> ValidateCurrentPageDocumentMatched();

    public TPage AssertPageDocumentLoaded();

    public TPage Reload();

    public List<IWebElement> AllErrors();

    public PlatformValidationResult<TPage> ValidateNoErrors();

    public TPage AssertNoErrors();

    public TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        double? maxWaitSeconds = null);

    public TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Action<TPage> stopWaitOnAssertError,
        double? maxWaitSeconds = null);

    public TResult WaitUntilAssertSuccess<TResult, TStopIfFailResult>(
        Func<TPage, TResult> waitForSuccess,
        Func<TPage, TStopIfFailResult> stopWaitOnAssertError,
        double? maxWaitSeconds = null);

    public static string BuildQueryParamsUrlPart(TPage page)
    {
        return page.QueryParams.PipeIfOrDefault(
            page.QueryParams?.Any() == true,
            _ => page.QueryParams.ToQueryString(),
            "");
    }

    public static string BuildBaseUrl(TPage page)
    {
        return $"{page.Origin}{page.Path}";
    }

    public static Uri BuildFullUrl(TPage page)
    {
        return new Uri($"{page.BaseUrl}{page.QueryParamsUrlPart}");
    }

    public static PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched(TPage page)
    {
        return page.Validate(
            must: page.WebDriver.Url.StartsWith(page.BaseUrl),
            Helper.AssertMsgBuilder.Failed(
                "Current Page Url is not matched",
                expected: page.BaseUrl,
                actual: page.WebDriver.Url));
    }

    public static PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched(TPage page)
    {
        return page.Validate(
            must: page.Title == page.WebDriver.Title,
            Helper.AssertMsgBuilder.Failed(
                "Current Page Title is not matched",
                expected: page.Title,
                actual: page.WebDriver.Title));
    }

    public static TPage Reload(TPage page)
    {
        page.ValidateCurrentPageUrlMatched().EnsureValid();

        page.WebDriver.Navigate().Refresh();

        return page;
    }

    public static List<IWebElement> AllErrors(TPage page, string errorElementSelector)
    {
        return page.WebDriver.FindElements(errorElementSelector)
            .Where(p => p.Displayed && p.Enabled && !p.Text.IsNullOrWhiteSpace())
            .ToList();
    }

    public static PlatformValidationResult<TPage> ValidateHasNoErrors(TPage page)
    {
        return page.AllErrors()
            .Validate(
                must: errors => !errors.Any(),
                errors => Helper.AssertMsgBuilder.Failed(
                    "Has Errors displayed on Page",
                    expected: "No Errors displayed on Page",
                    actual: errors.Select(p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }
}

public abstract class Page<TPage, TSettings> : UiComponent<TPage>, IPage<TPage, TSettings>
    where TPage : Page<TPage, TSettings>
    where TSettings : AutomationTestSettings
{
    public Page(IWebDriver webDriver, TSettings settings) : base(webDriver, directReferenceRootElement: null)
    {
        Settings = settings;
    }

    public abstract string ErrorElementCssSelector { get; }

    protected TSettings Settings { get; }
    protected virtual int DefaultWaitUntilMaxSeconds => Util.TaskRunner.DefaultWaitUntilMaxSeconds;

    public override string RootElementClassSelector => "body";
    public abstract string Title { get; }
    public abstract IWebElement? GlobalSpinnerElement { get; }
    public abstract string AppName { get; }
    public string Origin => Settings.AppNameToOrigin[AppName];
    public abstract string Path { get; }
    public string BaseUrl => IPage<TPage, TSettings>.BuildBaseUrl(this.As<TPage>());
    public Dictionary<string, string?>? QueryParams { get; set; }
    public string QueryParamsUrlPart => IPage<TPage, TSettings>.BuildQueryParamsUrlPart(this.As<TPage>());
    public Uri FullUrl => IPage<TPage, TSettings>.BuildFullUrl(this.As<TPage>());

    TSettings IPage<TPage, TSettings>.Settings()
    {
        return Settings;
    }

    public PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched()
    {
        return IPage<TPage, TSettings>.ValidateCurrentPageUrlMatched(this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched()
    {
        return IPage<TPage, TSettings>.ValidateCurrentPageTitleMatched(this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidateCurrentPageDocumentMatched()
    {
        return ValidateCurrentPageUrlMatched().And(() => ValidateCurrentPageTitleMatched());
    }

    public TPage Reload()
    {
        return IPage<TPage, TSettings>.Reload(this.As<TPage>());
    }

    public virtual List<IWebElement> AllErrors()
    {
        return IPage<TPage, TSettings>.AllErrors(this.As<TPage>(), ErrorElementCssSelector);
    }

    public PlatformValidationResult<TPage> ValidateNoErrors()
    {
        return IPage<TPage, TSettings>.ValidateHasNoErrors(this.As<TPage>());
    }

    public TPage AssertNoErrors()
    {
        return ValidateNoErrors().AssertValid();
    }

    public TPage AssertPageDocumentLoaded()
    {
        return ValidateCurrentPageDocumentMatched().AssertValid();
    }

    public virtual TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        double? maxWaitSeconds = null)
    {
        return this.As<TPage>().WaitUntilNoException(waitForSuccess, maxWaitSeconds ?? DefaultWaitUntilMaxSeconds);
    }

    public virtual TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Action<TPage> stopWaitOnAssertError,
        double? maxWaitSeconds = null)
    {
        return this.As<TPage>()
            .WaitUntilNoException(
                waitForSuccess,
                stopWaitOnAssertError: _ =>
                {
                    stopWaitOnAssertError(_);
                    return default(TResult);
                },
                maxWaitSeconds ?? DefaultWaitUntilMaxSeconds);
    }

    public virtual TResult WaitUntilAssertSuccess<TResult, TStopIfFailResult>(
        Func<TPage, TResult> waitForSuccess,
        Func<TPage, TStopIfFailResult> stopWaitOnAssertError,
        double? maxWaitSeconds = null)
    {
        return this.As<TPage>().WaitUntilNoException(waitForSuccess, stopWaitOnAssertError, maxWaitSeconds ?? DefaultWaitUntilMaxSeconds);
    }

    public virtual TPage WaitGlobalSpinnerStopped(
        int? maxWaitForLoadingDataSeconds = null,
        string waitForMsg = "Page Global Spinner is stopped")
    {
        return (TPage)this.WaitUntil(
            _ => GlobalSpinnerElement?.IsClickable() != true,
            maxWaitSeconds: maxWaitForLoadingDataSeconds ?? DefaultWaitUntilMaxSeconds, // Multiple wait time to test failed waiting timeout
            waitForMsg: waitForMsg);
    }
}
