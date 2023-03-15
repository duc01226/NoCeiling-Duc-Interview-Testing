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
        Dictionary<string, string?>? queryParams = null)
        where TPage : class, IPage<TPage, TSettings>
        where TSettings : AutomationTestSettings
    {
        var page = IPage.CreateInstance<TPage, TSettings>(webDriver, settings)
            .With(_ => _.QueryParams = queryParams);

        webDriver.Navigate().GoToUrl(page.FullUrl);

        return page;
    }

    public static IPage NavigatePageByFullUrl<TSettings>(
        this IWebDriver webDriver,
        Assembly pageAssembly,
        TSettings settings,
        string url)
        where TSettings : AutomationTestSettings
    {
        var page = IPage.CreateInstanceByMatchingUrl(pageAssembly, url, webDriver, settings);

        if (page != null)
            webDriver.Navigate().GoToUrl(page.FullUrl);
        else throw new Exception($"Not found any defined page class which match the given url {url}");

        return page;
    }

    public static IPage NavigatePageByUrlInfo<TSettings>(
        this IWebDriver webDriver,
        Assembly pageAssembly,
        TSettings settings,
        string fromPageAppName,
        string fromPagePath,
        string? queryParams = null)
        where TSettings : AutomationTestSettings
    {
        return NavigatePageByFullUrl(webDriver, pageAssembly, settings, IPage.BuildFullUrl(settings, fromPageAppName, fromPagePath, queryParams).AbsoluteUri);
    }

    public static TPage? TryGetCurrentActivePage<TPage, TSettings>(
        this IWebDriver webDriver,
        TSettings settings)
        where TPage : class, IPage<TPage, TSettings>
        where TSettings : AutomationTestSettings
    {
        var page = IPage.CreateInstance<TPage, TSettings>(webDriver, settings)
            .With(page => page.QueryParams = webDriver.Url.ToUri().QueryParams());

        return page.ValidateCurrentPageDocumentMatched() ? page : null;
    }

    public static TPage NavigatePage<TPage>(
        this IWebDriver webDriver,
        AutomationTestSettings settings,
        Dictionary<string, string?>? queryParams = null)
        where TPage : class, IPage<TPage, AutomationTestSettings>
    {
        return NavigatePage<TPage, AutomationTestSettings>(webDriver, settings, queryParams);
    }

    public static IWebElement FindElement(this IWebDriver webDriver, string cssSelector)
    {
        return webDriver.FindElement(By.CssSelector(cssSelector));
    }

    public static IWebElement? TryFindElement(this IWebDriver webDriver, string cssSelector)
    {
        return Util.TaskRunner.CatchException(() => webDriver.FindElement(By.CssSelector(cssSelector)), fallbackValue: null);
    }

    public static List<IWebElement> FindElements(this IWebDriver webDriver, string cssSelector)
    {
        return webDriver.FindElements(By.CssSelector(cssSelector)).ToList();
    }

    public static Actions StartActions(this IWebDriver webDriver)
    {
        return new Actions(webDriver);
    }

    public static IWebDriver PerformActions(this IWebDriver webDriver, Func<Actions, Actions> doActions)
    {
        doActions(webDriver.StartActions()).Perform();

        return webDriver;
    }

    public static IWebDriver Reset(this IWebDriver webDriver)
    {
        webDriver.Manage().Cookies.DeleteAllCookies();
        webDriver.Navigate().GoToUrl("about:blank");

        return webDriver;
    }
}
