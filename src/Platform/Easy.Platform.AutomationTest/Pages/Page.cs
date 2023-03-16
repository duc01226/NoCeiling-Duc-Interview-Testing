using System.Reflection;
using Easy.Platform.AutomationTest.Extensions;
using Easy.Platform.AutomationTest.Helpers;
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

    public IPage Reload();

    public static IPage<TSettings> CreateInstance<TSettings>(
        Type pageType,
        IWebDriver webDriver,
        TSettings settings,
        Dictionary<string, string?>? queryParams = null) where TSettings : AutomationTestSettings
    {
        return Util.CreateInstance(pageType, webDriver, settings)
            .Cast<IPage<TSettings>>()
            .With(_ => _.QueryParams = queryParams);
    }

    public static IPage<TSettings>? TryCreateInstance<TSettings>(
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
            .Select(selector: pageType => TryCreateInstance(pageType, webDriver, settings, queryParams: url.ToUri().QueryParams()))
            .FirstOrDefault(predicate: parsedPage => parsedPage?.Pipe(fn: _ => ValidateUrlMatchedForPage(_, url)).IsValid == true);
    }

    public static PlatformValidationResult<TPage> ValidateUrlMatchedForPage<TPage>(TPage page, string url)
        where TPage : IPage
    {
        return page.Validate(
            must: url.StartsWith(page.BaseUrl),
            AssertHelper.Failed(
                generalMsg: "Url is not matched",
                page.BaseUrl,
                url));
    }

    public static PlatformValidationResult<TPage> ValidateCurrentPageUrlMatched<TPage>(TPage page)
        where TPage : IPage
    {
        return page.Validate(
            must: page.WebDriver.Url.StartsWith(page.BaseUrl),
            AssertHelper.Failed(
                generalMsg: "Current Page Url is not matched",
                page.BaseUrl,
                page.WebDriver.Url));
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
        return Util.Path.ConcatRelativePath(origin, path);
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

    public static PlatformValidationResult<TPage> ValidatePageHasNoErrors<TPage>(TPage page) where TPage : IPage
    {
        if (page.ValidateIsCurrentActivePage() == false)
            return PlatformValidationResult.Valid(page);

        return page
            .AllErrorElements()
            .Validate(
                must: errorElements => !errorElements.Any(predicate: errorElement => errorElement.IsClickable()),
                errorMsgs: errors => AssertHelper.Failed(
                    generalMsg: "Has errors displayed on Page",
                    expected: "No errors displayed on Page",
                    actual: errors.Select(selector: p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }

    public static PlatformValidationResult<TPage> ValidatePageHasSomeErrors<TPage>(TPage page) where TPage : IPage
    {
        if (page.ValidateIsCurrentActivePage() == false)
            return PlatformValidationResult.Valid(page);

        return page
            .AllErrorElements()
            .Validate(
                must: errors => errors.Any(),
                errorMsgs: errors => AssertHelper.Failed(
                    generalMsg: "Has no errors displayed on Page",
                    expected: "Has some errors displayed on Page",
                    actual: errors.Select(selector: p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }

    public static PlatformValidationResult<TPage> ValidatePageMustHasErrors<TPage>(TPage page, string errorMsg) where TPage : IPage
    {
        if (page.ValidateIsCurrentActivePage() == false)
            return PlatformValidationResult.Valid(page);

        return page
            .AllErrorElements()
            .Validate(
                must: errors => errors.Any(predicate: p => p.Text.Contains(errorMsg, StringComparison.InvariantCultureIgnoreCase)),
                errorMsgs: errors => AssertHelper.Failed(
                    generalMsg: "Has no errors displayed on Page",
                    expected: $"Must has error \"{errorMsg}\" displayed on Page",
                    actual: errors.Select(selector: p => p.Text).JoinToString(Environment.NewLine)))
            .Of(page);
    }

    public List<IWebElement> AllErrorElements();

    public List<string> AllErrors();

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
        return ValidatePageHasNoErrors(page: this);
    }

    public PlatformValidationResult<IPage> ValidatePageHasSomeErrors()
    {
        return ValidatePageHasSomeErrors(page: this);
    }

    public PlatformValidationResult<IPage> ValidatePageMustHasError(string errorMsg)
    {
        return ValidatePageMustHasErrors(page: this, errorMsg);
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
}

public interface IPage<TSettings> : IPage where TSettings : AutomationTestSettings
{
    public TSettings Settings { get; }

    public new IPage<TSettings> Reload();

    public new IPage<TSettings> AssertPageHasNoErrors()
    {
        return this.As<IPage>().AssertPageHasNoErrors().As<IPage<TSettings>>();
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
        double? maxWaitSeconds = null);

    public TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Action<TPage> continueWaitOnlyWhen,
        double? maxWaitSeconds = null);

    public TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Func<TPage, object> continueWaitOnlyWhen,
        double? maxWaitSeconds = null);

    public TCurrentActivePage? TryGetCurrentActiveDefinedPage<TCurrentActivePage>() where TCurrentActivePage : class, IPage<TCurrentActivePage, TSettings>;

    public static TPage Reload(TPage page)
    {
        page.ValidateCurrentPageUrlMatched().EnsureValid();

        page.WebDriver.Navigate().Refresh();

        return page;
    }

    public static List<IWebElement> AllErrorElements(TPage page, string errorElementSelector)
    {
        return page.WebDriver.FindElements(errorElementSelector)
            .Where(predicate: p => p.Displayed && p.Enabled && !p.Text.IsNullOrWhiteSpace())
            .ToList();
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

    public virtual int DefaultWaitUntilMaxSeconds => Util.TaskRunner.DefaultWaitUntilMaxSeconds;

    public TSettings Settings { get; }

    public override string RootElementClassSelector => "body";
    public abstract string Title { get; }
    public abstract IWebElement? GlobalSpinnerElement { get; }

    IPage IPage.Reload()
    {
        return Reload();
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
    /// The path of the page after the origin. The full url is: {Origin}/{Path}{QueryParamsUrlPart}. See <see cref="IPage{TPage,TSettings}.BuildFullUrl" />
    /// </summary>
    public abstract string Path { get; }

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

    public virtual List<IWebElement> AllErrorElements()
    {
        return IPage<TPage, TSettings>.AllErrorElements(page: this.As<TPage>(), ErrorElementCssSelector);
    }

    public List<string> AllErrors()
    {
        return AllErrorElements().Select(selector: p => p.Text).ToList();
    }

    public PlatformValidationResult<TPage> ValidatePageHasNoErrors()
    {
        return IPage.ValidatePageHasNoErrors(page: this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidatePageHasSomeErrors()
    {
        return IPage.ValidatePageHasSomeErrors(page: this.As<TPage>());
    }

    public PlatformValidationResult<TPage> ValidatePageMustHasError(string errorMsg)
    {
        return IPage.ValidatePageMustHasErrors(page: this.As<TPage>(), errorMsg);
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
        double? maxWaitSeconds = null)
    {
        return this.As<TPage>().WaitUntilGetSuccess(waitForSuccess, maxWaitSeconds: maxWaitSeconds ?? DefaultWaitUntilMaxSeconds);
    }

    public virtual TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Action<TPage> continueWaitOnlyWhen,
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
                maxWaitSeconds: maxWaitSeconds ?? DefaultWaitUntilMaxSeconds);
    }

    public virtual TResult WaitUntilAssertSuccess<TResult>(
        Func<TPage, TResult> waitForSuccess,
        Func<TPage, object> continueWaitOnlyWhen,
        double? maxWaitSeconds = null)
    {
        return this.As<TPage>().WaitUntilGetSuccess(waitForSuccess, continueWaitOnlyWhen, maxWaitSeconds: maxWaitSeconds ?? DefaultWaitUntilMaxSeconds);
    }

    public TCurrentActivePage? TryGetCurrentActiveDefinedPage<TCurrentActivePage>() where TCurrentActivePage : class, IPage<TCurrentActivePage, TSettings>
    {
        return WebDriver.TryGetCurrentActiveDefinedPage<TCurrentActivePage, TSettings>(Settings);
    }

    public virtual TPage WaitGlobalSpinnerStopped(
        int? maxWaitForLoadingDataSeconds = null,
        string waitForMsg = "Page Global Spinner is stopped")
    {
        return (TPage)this.WaitUntil(
            condition: _ => GlobalSpinnerElement?.IsClickable() != true,
            maxWaitSeconds: maxWaitForLoadingDataSeconds ?? DefaultWaitUntilMaxSeconds, // Multiple wait time to test failed waiting timeout
            waitForMsg: waitForMsg);
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

    public override string ErrorElementCssSelector => ".error";
    public override string Title => WebDriver.Title;
    public override IWebElement? GlobalSpinnerElement { get; } = null;

    public override string AppName =>
        Settings.AppNameToOrigin.Where(predicate: p => WebDriver.Url.Contains(p.Value)).Select(selector: p => p.Key).FirstOrDefault() ?? "Unknown";

    public override string Path => WebDriver.Url.ToUri().Path();
    public override string Origin => WebDriver.Url.ToUri().Origin();
}

public class DefaultGeneralCurrentActivePage : GeneralCurrentActivePage<AutomationTestSettings>
{
    public DefaultGeneralCurrentActivePage(IWebDriver webDriver, AutomationTestSettings settings) : base(webDriver, settings)
    {
    }
}
