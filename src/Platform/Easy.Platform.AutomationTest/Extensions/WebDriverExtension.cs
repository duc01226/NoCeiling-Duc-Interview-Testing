using System.Reflection;
using Easy.Platform.AutomationTest.Pages;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;

namespace Easy.Platform.AutomationTest.Extensions;

public static class WebDriverExtension
{
    public static TPage NavigatePage<TPage, TSettings>(
        this IWebDriver webDriver,
        TSettings settings,
        Dictionary<string, string?>? queryParams = null,
        Dictionary<string, string>? routeParams = null
    )
        where TPage : class, IPage<TPage, TSettings>
        where TSettings : AutomationTestSettings
    {
        var page = IPage
            .CreateInstance<TPage, TSettings>(webDriver, settings)
            .With(_ => _.QueryParams = queryParams)
            .With(_ => _.PathRouteParams = routeParams);

        webDriver.Navigate().GoToUrl(page.FullUrl);

        return page;
    }

    public static IPage NavigatePageByFullUrl<TSettings>(this IWebDriver webDriver, Assembly definedPageAssembly, TSettings settings, string url)
        where TSettings : AutomationTestSettings
    {
        var page = IPage.CreateInstanceByMatchingUrl(definedPageAssembly, url, webDriver, settings);

        if (page != null)
            webDriver.Navigate().GoToUrl(page.FullUrl);
        else
            throw new Exception(message: $"Not found any defined page class which match the given url {url}");

        return page;
    }

    public static IPage NavigatePageByUrlInfo<TSettings>(
        this IWebDriver webDriver,
        Assembly definedPageAssembly,
        TSettings settings,
        string appName,
        string path,
        string? queryParams = null
    )
        where TSettings : AutomationTestSettings
    {
        return NavigatePageByUrlInfo(webDriver, definedPageAssembly, settings, appName, path, queryParams, fullUrl: out var _);
    }

    public static IPage NavigatePageByUrlInfo<TSettings>(
        this IWebDriver webDriver,
        Assembly definedPageAssembly,
        TSettings settings,
        string appName,
        string path,
        string? queryParams,
        out string fullUrl
    )
        where TSettings : AutomationTestSettings
    {
        fullUrl = IPage.BuildFullUrl(settings, appName, path, queryParams).AbsoluteUri;

        return NavigatePageByFullUrl(webDriver, definedPageAssembly, settings, fullUrl);
    }

    /// <summary>
    /// Try <see cref="GetCurrentActiveDefinedPage{TPage,TSettings}" /> or return null if no matched pages
    /// </summary>
    public static TPage? TryGetCurrentActiveDefinedPage<TPage, TSettings>(this IWebDriver webDriver, TSettings settings)
        where TPage : class, IPage<TPage, TSettings>
        where TSettings : AutomationTestSettings
    {
        try
        {
            return GetCurrentActiveDefinedPage<TPage, TSettings>(webDriver, settings);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Get current page which must match defined page with the give TPage type. Throw exception if no matched page.
    /// </summary>
    public static TPage GetCurrentActiveDefinedPage<TPage, TSettings>(this IWebDriver webDriver, TSettings settings)
        where TPage : class, IPage<TPage, TSettings>
        where TSettings : AutomationTestSettings
    {
        var page = IPage
            .CreateInstance<TPage, TSettings>(webDriver, settings)
            .With(page => page.QueryParams = webDriver.Url.ToUri().QueryParams())
            .With(page => page.PathRouteParams = IPage.BuildPathRouteParams(webDriver.Url.ToUri().Path(), page.PathRoute))
            .AssertIsCurrentActivePage();

        return page;
    }

    public static IPage<TSettings> GetCurrentActiveDefinedPage<TSettings>(this IWebDriver webDriver, TSettings settings, Assembly definedPageAssembly)
        where TSettings : AutomationTestSettings
    {
        return GetDefinedPageByUrl(webDriver, settings, definedPageAssembly, webDriver.Url);
    }

    public static IPage<TSettings>? TryGetCurrentActiveDefinedPage<TSettings>(
        this IWebDriver webDriver,
        TSettings settings,
        Assembly definedPageAssembly
    )
        where TSettings : AutomationTestSettings
    {
        try
        {
            return GetCurrentActiveDefinedPage(webDriver, settings, definedPageAssembly);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Get current page which must match defined page with the give url. Throw exception if no matched page.
    /// </summary>
    public static IPage<TSettings> GetDefinedPageByUrl<TSettings>(
        this IWebDriver webDriver,
        TSettings settings,
        Assembly definedPageAssembly,
        string url
    )
        where TSettings : AutomationTestSettings
    {
        var page = IPage
            .CreateInstanceByMatchingUrl(definedPageAssembly, url, webDriver, settings)
            .EnsureNotNull(exception: () => new Exception(message: $"Not found any defined page class which match the url '{url}'"))!;

        return page!;
    }

    /// <summary>
    /// Try <see cref="GetDefinedPageByUrl{TSettings}" /> or return null if no matched pages
    /// </summary>
    public static IPage? TryGetDefinedPageByUrl<TSettings>(this IWebDriver webDriver, TSettings settings, Assembly definedPageAssembly, string url)
        where TSettings : AutomationTestSettings
    {
        try
        {
            return GetDefinedPageByUrl(webDriver, settings, definedPageAssembly, url);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Get current page which must match defined page with the give AppName and Path. Throw exception if no matched page.
    /// </summary>
    public static IPage GetDefinedPageByUrlInfo<TSettings>(
        this IWebDriver webDriver,
        TSettings settings,
        Assembly definedPageAssembly,
        string appName,
        string path,
        string? queryParams = null
    )
        where TSettings : AutomationTestSettings
    {
        var page = IPage
            .CreateInstanceByMatchingUrl(
                definedPageAssembly,
                IPage.BuildFullUrl(settings, appName, path, queryParams).AbsoluteUri,
                webDriver,
                settings
            )
            .EnsureNotNull(
                exception: () =>
                    new Exception(message: $"Not found any defined page class which match the given info: AppName: {appName}; Path: {path};")
            )!;

        return page!;
    }

    /// <summary>
    /// Try <see cref="GetDefinedPageByUrlInfo{TSettings}" /> or return null if no matched pages
    /// </summary>
    public static IPage? TryGetDefinedPageByUrlInfo<TSettings>(
        this IWebDriver webDriver,
        TSettings settings,
        Assembly definedPageAssembly,
        string appName,
        string path,
        string? queryParams = null
    )
        where TSettings : AutomationTestSettings
    {
        try
        {
            return GetDefinedPageByUrlInfo(webDriver, settings, definedPageAssembly, appName, path, queryParams);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static GeneralCurrentActivePage<TSettings> GetGeneralCurrentActivePage<TSettings>(this IWebDriver webDriver, TSettings settings)
        where TSettings : AutomationTestSettings
    {
        return IPage.CreateInstance<GeneralCurrentActivePage<TSettings>, TSettings>(webDriver, settings);
    }

    public static TPage NavigatePage<TPage>(
        this IWebDriver webDriver,
        AutomationTestSettings settings,
        Dictionary<string, string?>? queryParams = null,
        Dictionary<string, string>? routeParams = null
    )
        where TPage : class, IPage<TPage, AutomationTestSettings>
    {
        return NavigatePage<TPage, AutomationTestSettings>(webDriver, settings, queryParams, routeParams);
    }

    public static IWebElement FindElement(this IWebDriver webDriver, string cssSelector)
    {
        return webDriver.FindElement(by: By.CssSelector(cssSelector));
    }

    public static IWebElement? TryFindElement(this IWebDriver webDriver, string cssSelector)
    {
        return Util.TaskRunner.CatchException(func: () => webDriver.FindElement(by: By.CssSelector(cssSelector)), fallbackValue: null);
    }

    public static List<IWebElement> FindElements(this IWebDriver webDriver, string cssSelector)
    {
        return webDriver.FindElements(by: By.CssSelector(cssSelector)).ToList();
    }

    public static Actions StartActions(this IWebDriver webDriver)
    {
        return new Actions(webDriver);
    }

    public static IWebDriver PerformActions(this IWebDriver webDriver, Func<Actions, Actions> doActions)
    {
        doActions(arg: webDriver.StartActions()).Perform();

        return webDriver;
    }

    public static IWebDriver Reset(this IWebDriver webDriver)
    {
        webDriver.Manage().Cookies.DeleteAllCookies();
        webDriver.Navigate().GoToUrl(url: "about:blank");

        return webDriver;
    }

    public static IWebDriver SwitchToBrowserTab(this IWebDriver webDriver, int tabIndex)
    {
        webDriver.SwitchTo().Window(webDriver.WindowHandles[tabIndex]);
        return webDriver;
    }
}
