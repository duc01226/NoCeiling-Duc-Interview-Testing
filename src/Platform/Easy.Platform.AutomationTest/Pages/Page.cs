using System.Reflection;
using Easy.Platform.AutomationTest.Extensions;
using Easy.Platform.AutomationTest.Helpers;
using Easy.Platform.AutomationTest.TestFrameworks.Xunit;
using Easy.Platform.AutomationTest.UiComponents;
using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest.Pages;

public interface IPage : IUiComponent
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

    public static IPage CreateInstance<TSettings>(
        Type pageType,
        IWebDriver webDriver,
        TSettings settings,
        Dictionary<string, string?>? queryParams = null) where TSettings : AutomationTestSettings
    {
        return Util.CreateInstance(pageType, webDriver, settings)
            .Cast<IPage>()
            .With(_ => _.QueryParams = queryParams);
    }

    public static IPage? TryCreateInstance<TSettings>(
        Type pageType,
        IWebDriver webDriver,
        TSettings settings,
        Dictionary<string, string?>? queryParams = null) where TSettings : AutomationTestSettings
    {
        try
        {
            return CreateInstance(pageType, webDriver, settings, queryParams);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static TPage CreateInstance<TPage, TSettings>(
        IWebDriver webDriver,
        TSettings settings,
        Dictionary<string, string?>? queryParams = null)
        where TPage : IPage<TPage, TSettings>
        where TSettings : AutomationTestSettings
    {
        return Util.CreateInstance<TPage>(webDriver, settings)
            .With(_ => _.QueryParams = queryParams);
    }

    public static IPage? CreateInstanceByMatchingUrl<TSettings>(
        Assembly pageAssembly,
        string url,
        IWebDriver webDriver,
        TSettings settings) where TSettings : AutomationTestSettings
    {
        return pageAssembly
            .GetTypes()
            .Where(
                scanType => scanType.IsClass &&
                            !scanType.IsAbstract &&
                            scanType.IsAssignableTo(typeof(IPage)))
            .Select(pageType => TryCreateInstance(pageType, webDriver, settings, url.ToUri().QueryParams()))
            .FirstOrDefault(parsedPage => parsedPage?.Pipe(_ => ValidateUrlMatchedForPage(_, url)).IsValid == true);
    }

    public static PlatformValidationResult<TPage> ValidateUrlMatchedForPage<TPage>(TPage page, string url)
        where TPage : IPage
    {
        return page.Validate(
            must: url.StartsWith(page.BaseUrl),
            Helper.AssertMsgBuilder.Failed(
                "Url is not matched",
                expected: page.BaseUrl,
                actual: url));
    }

    public static PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched<TPage>(TPage page)
        where TPage : IPage
    {
        return page.Validate(
            must: page.WebDriver.Url.StartsWith(page.BaseUrl),
            Helper.AssertMsgBuilder.Failed(
                "Current Page Url is not matched",
                expected: page.BaseUrl,
                actual: page.WebDriver.Url));
    }

    public static PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched<TPage>(TPage page)
        where TPage : IPage
    {
        return page.Validate(
            must: page.Title == page.WebDriver.Title,
            Helper.AssertMsgBuilder.Failed(
                "Current Page Title is not matched",
                expected: page.Title,
                actual: page.WebDriver.Title));
    }

    public static string BuildBaseUrl<TPage>(TPage page) where TPage : IPage
    {
        return BuildBaseUrl(page.Origin, page.Path);
    }

    public static string BuildBaseUrl(string origin, string path)
    {
        return Util.Path.ConcatRelativePath(origin, path);
    }

    public static Uri BuildFullUrl<TPage>(TPage page) where TPage : IPage
    {
        return BuildFullUrl(page.BaseUrl, page.QueryParamsUrlPart);
    }

    public static Uri BuildFullUrl(string baseUrl, string? queryParams = null)
    {
        var queryParamsPart = queryParams?.StartsWith("?") == true
            ? queryParams.Substring(1)
            : queryParams;
        return new Uri($"{baseUrl}{(!queryParamsPart.IsNullOrEmpty() ? "?" + queryParamsPart : "")}");
    }

    public static Uri BuildFullUrl(
        AutomationTestSettings settings,
        string appName,
        string path,
        string? queryParams = null)
    {
        return BuildFullUrl(BuildBaseUrl(BuildOrigin(settings, appName), path), queryParams);
    }

    public static string BuildQueryParamsUrlPart<TPage>(TPage page) where TPage : IPage
    {
        return page.QueryParams.PipeIfOrDefault(
            when: page.QueryParams?.Any() == true,
            thenPipe: _ => page.QueryParams.ToQueryString(),
            defaultValue: "");
    }

    public List<IWebElement> AllErrorElements();

    public List<string> AllErrors();

    public PlatformValidationResult<IPage> ValidateCurrentPageUrlMatched()
    {
        return ValidateCurrentPageUrlMatched(this);
    }

    public PlatformValidationResult<IPage> ValidateCurrentPageTitleMatched()
    {
        return ValidateCurrentPageTitleMatched(this);
    }

    public PlatformValidationResult<IPage> ValidateCurrentPageDocumentMatched()
    {
        return ValidateCurrentPageUrlMatched().And(() => ValidateCurrentPageTitleMatched());
    }

    public static string BuildOrigin<TSettings>(TSettings settings, string appName) where TSettings : AutomationTestSettings
    {
        return settings.AppNameToOrigin[appName];
    }
}

public interface IPage<TPage, TSettings> : IPage, IUiComponent
    where TPage : IPage<TPage, TSettings> where TSettings : AutomationTestSettings
{
    public TSettings Settings();

    public new PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched();

    public new PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched();

    public new PlatformValidationResult<TPage> ValidateCurrentPageDocumentMatched();

    public TPage AssertPageDocumentLoaded();

    public TPage Reload();

    public PlatformValidationResult<TPage> ValidateNoErrors();

    public PlatformValidationResult<TPage> ValidatePageMustHasError(string errorMsg);

    public TPage AssertPageNoErrors();

    public TPage AssertPageMustHasError(string errorMsg);

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

    public TCurrentActivePage? TryGetCurrentActivePage<TCurrentActivePage>() where TCurrentActivePage : class, IPage<TCurrentActivePage, TSettings>;

    public static TPage Reload(TPage page)
    {
        page.ValidateCurrentPageUrlMatched().EnsureValid();

        page.WebDriver.Navigate().Refresh();

        return page;
    }

    public static List<IWebElement> AllErrorElements(TPage page, string errorElementSelector)
    {
        return page.WebDriver.FindElements(errorElementSelector)
            .Where(p => p.Displayed && p.Enabled && !p.Text.IsNullOrWhiteSpace())
            .ToList();
    }

    public static PlatformValidationResult<TPage> ValidatePageHasNoErrors(TPage page)
    {
        return page.AllErrorElements()
            .Validate(
                must: errors => !errors.Any(),
                errors => Helper.AssertMsgBuilder.Failed(
                    "Has errors displayed on Page",
                    expected: "No errors displayed on Page",
                    actual: errors.Select(p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }

    public static PlatformValidationResult<TPage> ValidatePageMustHasErrors(TPage page, string errorMsg)
    {
        return page.AllErrorElements()
            .Validate(
                must: errors => errors.Any(p => p.Text.Contains(errorMsg, StringComparison.InvariantCultureIgnoreCase)),
                errors => Helper.AssertMsgBuilder.Failed(
                    "Has no errors displayed on Page",
                    expected: $"Must has error \"{errorMsg}\" displayed on Page",
                    actual: errors.Select(p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }
}

public abstract class Page<TPage, TSettings> : UiComponent<TPage>, IPage<TPage, TSettings>
    where TPage : Page<TPage, TSettings>, IPage<TPage, TSettings>
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

    /// <summary>
    /// Used to map from app name to the origin host url of the app. Used for <see cref="Origin" />
    /// </summary>
    public abstract string AppName { get; }

    /// <summary>
    /// Origin host url of the application, not including path
    /// </summary>
    public string Origin => IPage.BuildOrigin(Settings, AppName);

    /// <summary>
    /// The path of the page after the origin. The full url is: {Origin}/{Path}{QueryParamsUrlPart}. See <see cref="IPage{TPage,TSettings}.BuildFullUrl" />
    /// </summary>
    public abstract string Path { get; }

    public string BaseUrl => IPage.BuildBaseUrl(this.As<TPage>());
    public Dictionary<string, string?>? QueryParams { get; set; }
    public string QueryParamsUrlPart => IPage.BuildQueryParamsUrlPart(this.As<TPage>());
    public Uri FullUrl => IPage.BuildFullUrl(this.As<TPage>());

    TSettings IPage<TPage, TSettings>.Settings()
    {
        return Settings;
    }

    public PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched()
    {
        return IPage.ValidateCurrentPageUrlMatched(this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched()
    {
        return IPage.ValidateCurrentPageTitleMatched(this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidateCurrentPageDocumentMatched()
    {
        return ValidateCurrentPageUrlMatched().And(() => ValidateCurrentPageTitleMatched());
    }

    public TPage Reload()
    {
        return IPage<TPage, TSettings>.Reload(this.As<TPage>());
    }

    public virtual List<IWebElement> AllErrorElements()
    {
        return IPage<TPage, TSettings>.AllErrorElements(this.As<TPage>(), ErrorElementCssSelector);
    }

    public List<string> AllErrors()
    {
        return AllErrorElements().Select(p => p.Text).ToList();
    }

    public PlatformValidationResult<TPage> ValidateNoErrors()
    {
        return IPage<TPage, TSettings>.ValidatePageHasNoErrors(this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidatePageMustHasError(string errorMsg)
    {
        return IPage<TPage, TSettings>.ValidatePageMustHasErrors(this.As<TPage>(), errorMsg);
    }

    public TPage AssertPageNoErrors()
    {
        return ValidateNoErrors().AssertValid();
    }

    public TPage AssertPageMustHasError(string errorMsg)
    {
        return ValidatePageMustHasError(errorMsg).AssertValid();
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

    public TCurrentActivePage? TryGetCurrentActivePage<TCurrentActivePage>() where TCurrentActivePage : class, IPage<TCurrentActivePage, TSettings>
    {
        return WebDriver.TryGetCurrentActivePage<TCurrentActivePage, TSettings>(Settings);
    }

    public static TPage CreateInstance(
        IWebDriver webDriver,
        TSettings settings,
        Dictionary<string, string?>? queryParams = null)
    {
        return Util.CreateInstance<TPage>(webDriver, settings)
            .With(_ => _.QueryParams = queryParams);
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
