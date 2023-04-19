using System.Reflection;
using System.Text.RegularExpressions;
using Easy.Platform.AutomationTest.Extensions;
using Easy.Platform.AutomationTest.Helpers;
using Easy.Platform.AutomationTest.UiComponents;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace Easy.Platform.AutomationTest.Pages;

public interface IPage : IUiComponent
{
    public static readonly Regex BuildPathRouteParamRegex = new(@"\{(\w*?)\}", RegexOptions.Compiled);

    public string AppName { get; }
    public string Origin { get; }

    /// <summary>
    /// Used to build the path of the page after the origin. The full url is: {Origin}/{Path}{QueryParamsUrlPart}. See <see cref="BuildFullUrl{TPage}" />. <br/>
    /// Example: /Users; /Users/{id}/Detail
    /// </summary>
    public string PathRoute { get; }

    public Dictionary<string, string>? PathRouteParams { get; set; }

    public string Path { get; }

    /// <summary>
    /// The Base default url of a page without query
    /// </summary>
    public string BaseUrl { get; }

    public Dictionary<string, string?>? QueryParams { get; set; }

    public string QueryParamsUrlPart { get; }

    public Uri FullUrl { get; }

    public string Title { get; }

    public IWebElement? GlobalSpinnerElement { get; }

    /// <summary>
    /// Default max wait seconds value is used for all .WaitUntil method from a page 
    /// when max wait is not given
    /// </summary>
    public int DefaultMaxWaitSeconds { get; }

    public static string GetPathRouteParamName(Match pathRouteParamMatch)
    {
        return pathRouteParamMatch.Groups[1].Value;
    }

    public IPage Reload();

    public IEnumerable<IWebElement> GeneralErrorElements();

    public IEnumerable<IWebElement> FormValidationErrorElements();

    public IEnumerable<IWebElement> AllErrorElements();

    public IEnumerable<string> AllErrors();

    public PlatformValidationResult<IPage> ValidateCurrentPageUrlMatched()
    {
        return ValidateCurrentPageUrlMatched(page: this);
    }

    public PlatformValidationResult<IPage> ValidateCurrentPageTitleMatched()
    {
        return ValidateCurrentPageTitleMatched(page: this);
    }

    public PlatformValidationResult<IPage> ValidateIsCurrentActivePage()
    {
        return ValidateCurrentPageUrlMatched().And(nextValidation: () => ValidateCurrentPageTitleMatched());
    }

    public PlatformValidationResult<IPage> ValidatePageHasNoErrors()
    {
        return ValidatePageHasNoErrors(page: this, p => p.AllErrorElements());
    }

    public PlatformValidationResult<IPage> ValidatePageHasSomeErrors()
    {
        return ValidatePageHasSomeErrors(page: this, p => p.AllErrorElements());
    }

    public PlatformValidationResult<IPage> ValidatePageMustHasError(string errorMsg)
    {
        return ValidatePageMustHasErrors(page: this, p => p.AllErrorElements(), errorMsg);
    }

    public IPage AssertPageHasNoErrors()
    {
        return ValidatePageHasNoErrors().AssertValid();
    }

    public IPage AssertPageHasSomeErrors()
    {
        return ValidatePageHasSomeErrors().AssertValid();
    }

    public IPage AssertPageMustHasError(string errorMsg)
    {
        return ValidatePageMustHasError(errorMsg).AssertValid();
    }

    public IPage AssertIsCurrentActivePage()
    {
        return ValidateIsCurrentActivePage().AssertValid();
    }

    public static IPage<TSettings> CreateInstance<TSettings>(
        Type pageType,
        IWebDriver webDriver,
        TSettings settings,
        Dictionary<string, string?>? queryParams = null,
        string? generatedWithRouteParamsPath = null) where TSettings : AutomationTestSettings
    {
        return Util.CreateInstance(pageType, webDriver, settings)
            .Cast<IPage<TSettings>>()
            .With(_ => _.QueryParams = queryParams)
            .WithIf(generatedWithRouteParamsPath != null, _ => _.PathRouteParams = BuildPathRouteParams(generatedWithRouteParamsPath!, _.PathRoute));
    }

    public static Dictionary<string, string> BuildPathRouteParams(string generatedWithRouteParamsPath, string pathRoute)
    {
        var generatedWithRouteParamsPathSegments = generatedWithRouteParamsPath.TrimStart('/').Split("/");
        var pathRouteSegments = pathRoute.TrimStart('/').Split("/");

        return generatedWithRouteParamsPathSegments
            .Select(
                (generatedWithRouteParamsPathSegment, index) => new
                {
                    generatedWithRouteParamsPathSegment,
                    pathRouteSegmentMatch = BuildPathRouteParamRegex.Matches(pathRouteSegments[index]).FirstOrDefault()
                })
            .Where(p => p.pathRouteSegmentMatch != null)
            .ToDictionary(p => GetPathRouteParamName(p.pathRouteSegmentMatch!), p => p.generatedWithRouteParamsPathSegment);
    }

    public static IPage<TSettings>? TryCreateInstance<TSettings>(
        Type pageType,
        IWebDriver webDriver,
        TSettings settings,
        Dictionary<string, string?>? queryParams = null,
        string? generatedWithRouteParamsPath = null) where TSettings : AutomationTestSettings
    {
        try
        {
            return CreateInstance(pageType, webDriver, settings, queryParams, generatedWithRouteParamsPath);
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

    public static IPage<TSettings>? CreateInstanceByMatchingUrl<TSettings>(
        Assembly pageAssembly,
        string url,
        IWebDriver webDriver,
        TSettings settings) where TSettings : AutomationTestSettings
    {
        return pageAssembly
            .GetTypes()
            .Where(
                predicate: scanType => scanType.IsClass &&
                                       !scanType.IsAbstract &&
                                       scanType.IsAssignableTo(targetType: typeof(IPage)))
            .Select(
                selector: pageType => TryCreateInstance(
                    pageType,
                    webDriver,
                    settings,
                    queryParams: url.ToUri().QueryParams(),
                    generatedWithRouteParamsPath: url.ToUri().Path()))
            .OrderByDescending(p => p?.PathRoute.Length)
            .FirstOrDefault(predicate: parsedPage => parsedPage?.Pipe(fn: _ => ValidateUrlMatchedForPage(_, url)).IsValid == true);
    }

    public static PlatformValidationResult<TPage> ValidateUrlMatchedForPage<TPage>(TPage page, string url, string? generalErrorMsg = null)
        where TPage : IPage
    {
        var urlOrigin = url.ToUri().Origin();
        var urlPathSegments = url.ToUri().Path().TrimStart('/').Split("/");
        var pageUrlOrigin = page.BaseUrl.ToUri().Origin();
        var pageUrlPathSegments = page.BaseUrl.ToUri().Path().TrimStart('/').Split("/");

        return page.Validate(
            must: urlOrigin == pageUrlOrigin &&
                  urlPathSegments.Length == pageUrlPathSegments.Length &&
                  urlPathSegments.All(
                      (urlPathSegment, index) => urlPathSegment == pageUrlPathSegments[index] || BuildPathRouteParamRegex.IsMatch(pageUrlPathSegments[index])),
            AssertHelper.Failed(
                generalMsg: generalErrorMsg ?? "Page Url is not matched",
                page.BaseUrl,
                url));
    }

    public static PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched<TPage>(TPage page)
        where TPage : IPage
    {
        return ValidateUrlMatchedForPage(page, page.WebDriver.Url, "Current Page Url is not matched");
    }

    public static PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched<TPage>(TPage page)
        where TPage : IPage
    {
        return page.Validate(
            must: page.Title == page.WebDriver.Title,
            AssertHelper.Failed(
                generalMsg: "Current Page Title is not matched",
                page.Title,
                page.WebDriver.Title));
    }

    public static string BuildBaseUrl<TPage>(TPage page) where TPage : IPage
    {
        return BuildBaseUrl(page.Origin, page.Path);
    }

    public static string BuildBaseUrl(string origin, string path)
    {
        return Util.PathBuilder.ConcatRelativePath(origin, path);
    }

    public static string BuildPath<TPage>(TPage page) where TPage : IPage
    {
        return BuildPath(page.PathRoute, page.PathRouteParams);
    }

    /// <summary>
    /// Example: pagePathRoute: "/Users/{id}/Detail"; pagePathRouteParams: ["1"]; Return: /Users/1/Detail
    /// </summary>
    public static string BuildPath(string pagePathRoute, Dictionary<string, string>? pagePathRouteParams)
    {
        if (pagePathRouteParams == null || pagePathRouteParams.Count == 0) return pagePathRoute;

        return BuildPathRouteParamRegex.Replace(
            pagePathRoute,
            match => pagePathRouteParams![GetPathRouteParamName(match)]);
    }

    public static Uri BuildFullUrl<TPage>(TPage page) where TPage : IPage
    {
        return BuildFullUrl(page.BaseUrl, page.QueryParamsUrlPart);
    }

    public static Uri BuildFullUrl(string baseUrl, string? queryParams = null)
    {
        var queryParamsPart = queryParams?.StartsWith(value: "?") == true
            ? queryParams.Substring(startIndex: 1)
            : queryParams;
        return new Uri(uriString: $"{baseUrl}{(!queryParamsPart.IsNullOrEmpty() ? "?" + queryParamsPart : "")}");
    }

    public static Uri BuildFullUrl(
        AutomationTestSettings settings,
        string appName,
        string path,
        string? queryParams = null)
    {
        return BuildFullUrl(baseUrl: BuildBaseUrl(origin: BuildOrigin(settings, appName), path), queryParams);
    }

    public static string BuildQueryParamsUrlPart<TPage>(TPage page) where TPage : IPage
    {
        return page.QueryParams.PipeIfOrDefault(
            when: page.QueryParams?.Any() == true,
            thenPipe: _ => page.QueryParams.ToQueryString(),
            defaultValue: "");
    }

    public static string BuildOrigin<TSettings>(TSettings settings, string appName) where TSettings : AutomationTestSettings
    {
        if (settings.AppNameToOrigin.ContainsKey(appName) == false)
            throw new Exception(message: $"AppName: '{appName}' is invalid. It's not defined in settings.AppNameToOrigin");

        return settings.AppNameToOrigin[appName];
    }

    public static PlatformValidationResult<TPage> ValidatePageHasNoErrors<TPage>(TPage page, Func<TPage, IEnumerable<IWebElement>> pageErrors) where TPage : IPage
    {
        if (page.ValidateIsCurrentActivePage() == false)
            return PlatformValidationResult.Valid(page);

        return pageErrors(page)
            .Validate(
                must: errorElements => !errorElements.Any(predicate: errorElement => errorElement.IsClickable()),
                errorMsg: errors => AssertHelper.Failed(
                    generalMsg: "Has errors displayed on Page",
                    expected: "No errors displayed on Page",
                    actual: errors.Select(selector: p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }

    public static PlatformValidationResult<TPage> ValidatePageHasSomeErrors<TPage>(TPage page, Func<TPage, IEnumerable<IWebElement>> pageErrors) where TPage : IPage
    {
        if (page.ValidateIsCurrentActivePage() == false)
            return PlatformValidationResult.Valid(page);

        return pageErrors(page)
            .Validate(
                must: errors => errors.Any(),
                errorMsg: errors => AssertHelper.Failed(
                    generalMsg: "Has no errors displayed on Page",
                    expected: "Has some errors displayed on Page",
                    actual: errors.Select(selector: p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }

    public static PlatformValidationResult<TPage> ValidatePageMustHasErrors<TPage>(TPage page, Func<TPage, IEnumerable<IWebElement>> pageErrors, string errorMsg)
        where TPage : IPage
    {
        if (page.ValidateIsCurrentActivePage() == false)
            return PlatformValidationResult.Valid(page);

        return pageErrors(page)
            .Validate(
                must: errors => errors.Any(predicate: p => p.Text.ContainsIgnoreCase(errorMsg)),
                errorMsg: errors => AssertHelper.Failed(
                    generalMsg: "Has no errors displayed on Page",
                    expected: $"Must has error \"{errorMsg}\" displayed on Page",
                    actual: errors.Select(selector: p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }


    public static IEnumerable<IWebElement> GetErrorElements(IPage page, string? errorElementSelector)
    {
        return errorElementSelector.IsNullOrEmpty()
            ? Enumerable.Empty<IWebElement>()
            : page.WebDriver.FindElements(errorElementSelector!)
                .Where(predicate: p => p.IsClickable() && !p.Text.IsNullOrWhiteSpace());
    }
}

public interface IPage<TSettings> : IPage where TSettings : AutomationTestSettings
{
    public TSettings Settings { get; }

    public new IPage<TSettings> Reload();

    public new IPage<TSettings> AssertPageHasNoErrors()
    {
        return this.As<IPage>().AssertPageHasNoErrors().As<IPage<TSettings>>();
    }

    public new IPage<TSettings> AssertPageHasSomeErrors()
    {
        return this.As<IPage>().AssertPageHasSomeErrors().As<IPage<TSettings>>();
    }

    public new IPage<TSettings> AssertPageMustHasError(string errorMsg)
    {
        return this.As<IPage>().AssertPageMustHasError(errorMsg).As<IPage<TSettings>>();
    }

    public new IPage<TSettings> AssertIsCurrentActivePage()
    {
        return this.As<IPage>().AssertIsCurrentActivePage().As<IPage<TSettings>>();
    }
}

public interface IPage<TPage, TSettings> : IPage<TSettings>
    where TPage : IPage<TPage, TSettings> where TSettings : AutomationTestSettings
{
    public new PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched();

    public new PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched();

    public new PlatformValidationResult<TPage> ValidateIsCurrentActivePage();

    public new TPage AssertIsCurrentActivePage();

    public new TPage Reload();

    public new PlatformValidationResult<TPage> ValidatePageHasNoErrors();

    public new PlatformValidationResult<TPage> ValidatePageHasSomeErrors();

    public new PlatformValidationResult<TPage> ValidatePageMustHasError(string errorMsg);

    public new TPage AssertPageHasNoErrors();

    public new TPage AssertPageHasSomeErrors();

    public new TPage AssertPageMustHasError(string errorMsg);

    public TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        string? waitForMsg = null,
        double? maxWaitSeconds = null);

    public TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Action<TPage> continueWaitOnlyWhen,
        string? waitForMsg = null,
        double? maxWaitSeconds = null);

    public TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Func<TPage, object> continueWaitOnlyWhen,
        string? waitForMsg = null,
        double? maxWaitSeconds = null);

    public TCurrentActivePage? TryGetCurrentActiveDefinedPage<TCurrentActivePage>() where TCurrentActivePage : class, IPage<TCurrentActivePage, TSettings>;

    public static TPage Reload(TPage page)
    {
        page.ValidateCurrentPageUrlMatched().EnsureValid();

        page.WebDriver.Navigate().Refresh();

        return page;
    }
}

public abstract class Page<TPage, TSettings> : UiComponent<TPage>, IPage<TPage, TSettings>
    where TPage : Page<TPage, TSettings>, IPage<TPage, TSettings>
    where TSettings : AutomationTestSettings
{
    private WebDriverWait? webDriverWait;

    public Page(IWebDriver webDriver, TSettings settings) : base(webDriver, directReferenceRootElement: null)
    {
        Settings = settings;
    }

    public WebDriverWait WebDriverWait => webDriverWait ??= CreateDefaultWebDriverWait(WebDriver);
    public abstract string? GeneralErrorElementsCssSelector { get; }
    public abstract string? FormValidationErrorElementsCssSelector { get; }

    /// <summary>
    /// Used to WaitPageContentLoadedSuccessfully. Page is considered as loaded when this element from PageContentLoadedElementIndicatorSelector is displayed
    /// </summary>
    public abstract string PageContentLoadedElementIndicatorSelector { get; }

    public virtual int DefaultMaxWaitSeconds => Util.TaskRunner.DefaultWaitUntilMaxSeconds;

    public TSettings Settings { get; }

    public override string RootElementClassSelector => "body";
    public abstract string Title { get; }
    public abstract IWebElement? GlobalSpinnerElement { get; }

    IPage IPage.Reload()
    {
        return Reload();
    }

    public IEnumerable<IWebElement> GeneralErrorElements()
    {
        return IPage.GetErrorElements(this, GeneralErrorElementsCssSelector);
    }

    public IEnumerable<IWebElement> FormValidationErrorElements()
    {
        return IPage.GetErrorElements(this, FormValidationErrorElementsCssSelector);
    }

    public IEnumerable<IWebElement> AllErrorElements()
    {
        return GeneralErrorElements()
            .ConcatIf(
                GeneralErrorElementsCssSelector != FormValidationErrorElementsCssSelector,
                FormValidationErrorElements());
    }

    /// <summary>
    /// Used to map from app name to the origin host url of the app. Used for <see cref="Origin" />
    /// </summary>
    public abstract string AppName { get; }

    /// <summary>
    /// Origin host url of the application, not including path
    /// </summary>
    public virtual string Origin => IPage.BuildOrigin(Settings, AppName);

    /// <summary>
    /// The path of the page after the origin. The full url is: {Origin}/{Path}{QueryParamsUrlPart}. See <see cref="IPage.BuildFullUrl{TPage}" />
    /// </summary>
    public virtual string Path => IPage.BuildPath(this);

    public abstract string PathRoute { get; }
    public Dictionary<string, string>? PathRouteParams { get; set; }

    public string BaseUrl => IPage.BuildBaseUrl(page: this.As<TPage>());
    public Dictionary<string, string?>? QueryParams { get; set; }
    public string QueryParamsUrlPart => IPage.BuildQueryParamsUrlPart(page: this.As<TPage>());
    public Uri FullUrl => IPage.BuildFullUrl(page: this.As<TPage>());

    public PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched()
    {
        return IPage.ValidateCurrentPageUrlMatched(page: this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidateCurrentPageTitleMatched()
    {
        return IPage.ValidateCurrentPageTitleMatched(page: this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidateIsCurrentActivePage()
    {
        return ValidateCurrentPageUrlMatched().And(nextValidation: () => ValidateCurrentPageTitleMatched());
    }

    public TPage Reload()
    {
        return IPage<TPage, TSettings>.Reload(page: this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidatePageHasNoErrors()
    {
        return IPage.ValidatePageHasNoErrors(page: this.As<TPage>(), p => p.AllErrorElements());
    }

    public PlatformValidationResult<TPage> ValidatePageHasSomeErrors()
    {
        return IPage.ValidatePageHasSomeErrors(page: this.As<TPage>(), p => p.AllErrorElements());
    }

    public PlatformValidationResult<TPage> ValidatePageMustHasError(string errorMsg)
    {
        return IPage.ValidatePageMustHasErrors(page: this.As<TPage>(), p => p.AllErrorElements(), errorMsg);
    }

    IPage<TSettings> IPage<TSettings>.Reload()
    {
        return Reload();
    }

    public TPage AssertPageHasNoErrors()
    {
        return ValidatePageHasNoErrors().AssertValid();
    }

    public TPage AssertPageHasSomeErrors()
    {
        return ValidatePageHasSomeErrors().AssertValid();
    }

    public TPage AssertPageMustHasError(string errorMsg)
    {
        return ValidatePageMustHasError(errorMsg).AssertValid();
    }

    public TPage AssertIsCurrentActivePage()
    {
        return ValidateIsCurrentActivePage().AssertValid();
    }

    public virtual TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        string? waitForMsg = null,
        double? maxWaitSeconds = null)
    {
        return this.As<TPage>()
            .WaitUntilGetSuccess(
                waitForSuccess,
                waitForMsg: waitForMsg,
                maxWaitSeconds: maxWaitSeconds ?? DefaultMaxWaitSeconds);
    }

    public virtual TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Action<TPage> continueWaitOnlyWhen,
        string? waitForMsg = null,
        double? maxWaitSeconds = null)
    {
        return this.As<TPage>()
            .WaitUntilGetSuccess(
                waitForSuccess,
                continueWaitOnlyWhen: _ =>
                {
                    continueWaitOnlyWhen(_);
                    return default(TResult);
                },
                maxWaitSeconds: maxWaitSeconds ?? DefaultMaxWaitSeconds,
                waitForMsg);
    }

    public TCurrentActivePage? TryGetCurrentActiveDefinedPage<TCurrentActivePage>() where TCurrentActivePage : class, IPage<TCurrentActivePage, TSettings>
    {
        return WebDriver.TryGetCurrentActiveDefinedPage<TCurrentActivePage, TSettings>(Settings);
    }

    public IEnumerable<string> AllErrors()
    {
        return AllErrorElements().Select(selector: p => p.Text);
    }

    public virtual TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Func<TPage, object> continueWaitOnlyWhen,
        string? waitForMsg = null,
        double? maxWaitSeconds = null)
    {
        return this.As<TPage>()
            .WaitUntilGetSuccess(
                waitForSuccess,
                continueWaitOnlyWhen,
                maxWaitSeconds: maxWaitSeconds ?? DefaultMaxWaitSeconds,
                waitForMsg);
    }

    protected virtual WebDriverWait CreateDefaultWebDriverWait(IWebDriver webDriver)
    {
        return new WebDriverWait(webDriver, DefaultMaxWaitSeconds.Seconds())
            .With(p => p.PollingInterval = TimeSpan.FromMilliseconds(300));
    }

    public virtual TPage WaitGlobalSpinnerStopped(
        int? maxWaitForLoadingDataSeconds = null,
        string waitForMsg = "Page Global Spinner is stopped")
    {
        return WaitUntil(
            until: _ => GlobalSpinnerElement?.IsClickable() != true,
            maxWaitSeconds: maxWaitForLoadingDataSeconds ?? DefaultMaxWaitSeconds,
            waitForMsg: waitForMsg);
    }

    public virtual TPage WaitPageContentLoadedSuccessfully(
        int? maxWaitForLoadingDataSeconds = null,
        string waitForMsg = "Page Content is loaded and displayed successfully")
    {
        return WaitUntil(
                until: _ => WebDriver.TryFindElement(PageContentLoadedElementIndicatorSelector)?.Displayed == true,
                maxWaitSeconds: maxWaitForLoadingDataSeconds ?? DefaultMaxWaitSeconds,
                waitForMsg: waitForMsg)
            .WaitGlobalSpinnerStopped(maxWaitForLoadingDataSeconds);
    }

    public TPage WaitUntil(
        Func<TPage, bool> until,
        Func<TPage, object> continueWaitOnlyWhen,
        string? waitForMsg = null,
        double? maxWaitSeconds = null)
    {
        return Util.TaskRunner.WaitUntil(this.As<TPage>(), () => until(this.As<TPage>()), continueWaitOnlyWhen, maxWaitSeconds ?? DefaultMaxWaitSeconds, waitForMsg);
    }

    public TResult WaitUntilGetSuccess<TResult, TAny>(
        Func<TPage, TResult?> getResult,
        Func<TPage, TAny>? continueWaitOnlyWhen = null,
        string? waitForMsg = null,
        double? maxWaitSeconds = null)
    {
        return Util.TaskRunner.WaitUntilGetSuccess(
            this.As<TPage>(),
            getResult: p => getResult(this.As<TPage>()),
            continueWaitOnlyWhen,
            maxWaitSeconds ?? DefaultMaxWaitSeconds,
            waitForMsg);
    }

    public TPage WaitUntil(
        Func<TPage, bool> until,
        string? waitForMsg = null,
        double? maxWaitSeconds = null)
    {
        Util.TaskRunner.WaitUntil(() => until(this.As<TPage>()), maxWaitSeconds ?? DefaultMaxWaitSeconds, waitForMsg: waitForMsg);

        return this.As<TPage>();
    }

    public TPage WaitRetryDoUntil(
        Action<TPage> action,
        Func<TPage, bool> until,
        string? waitForMsg = null,
        double? maxWaitSeconds = null)
    {
        Util.TaskRunner.WaitRetryDoUntil(
            action: () => action(this.As<TPage>()),
            until: () => until(this.As<TPage>()),
            maxWaitSeconds ?? DefaultMaxWaitSeconds,
            waitForMsg: waitForMsg);

        return this.As<TPage>();
    }

    protected GeneralUiComponent CreateGeneralComponent(string rootElementSelector)
    {
        return new GeneralUiComponent(WebDriver, rootElementSelector: rootElementSelector, parent: this);
    }

    protected GeneralUiComponent CreateGeneralComponent(Func<IWebElement> directReferenceRootElement)
    {
        return new GeneralUiComponent(WebDriver, directReferenceRootElement: directReferenceRootElement, parent: this);
    }
}

/// <summary>
/// Page which always match with current active web page
/// </summary>
public class GeneralCurrentActivePage<TSettings> : Page<GeneralCurrentActivePage<TSettings>, TSettings>
    where TSettings : AutomationTestSettings
{
    public GeneralCurrentActivePage(IWebDriver webDriver, TSettings settings) : base(webDriver, settings)
    {
    }

    public override string Title => WebDriver.Title;
    public override IWebElement? GlobalSpinnerElement { get; } = null;
    public override string? GeneralErrorElementsCssSelector => ".error";
    public override string? FormValidationErrorElementsCssSelector => ".error";
    public override string PageContentLoadedElementIndicatorSelector => "body";

    public override string AppName =>
        Settings.AppNameToOrigin.Where(predicate: p => WebDriver.Url.Contains(p.Value)).Select(selector: p => p.Key).FirstOrDefault() ?? "Unknown";

    public override string Path => WebDriver.Url.ToUri().Path();
    public override string PathRoute => WebDriver.Url.ToUri().Path();
    public override string Origin => WebDriver.Url.ToUri().Origin();
}

public class DefaultGeneralCurrentActivePage : GeneralCurrentActivePage<AutomationTestSettings>
{
    public DefaultGeneralCurrentActivePage(IWebDriver webDriver, AutomationTestSettings settings) : base(webDriver, settings)
    {
    }
}
