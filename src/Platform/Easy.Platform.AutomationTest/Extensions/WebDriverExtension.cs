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
        var page = Util.CreateInstance<TPage>(webDriver, settings)
            .With(_ => _.QueryParams = queryParams);

        webDriver.Navigate().GoToUrl(page.FullUrl);

        return page;
    }

    public static TPage? TryGetCurrentActivePage<TPage, TSettings>(
        this IWebDriver webDriver,
        TSettings settings)
        where TPage : class, IPage<TPage, TSettings>
        where TSettings : AutomationTestSettings
    {
        var page = Util.CreateInstance<TPage>(webDriver, settings)
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

    public static TPage? TryGetCurrentActivePage<TPage>(
        this IWebDriver webDriver,
        AutomationTestSettings settings)
        where TPage : class, IPage<TPage, AutomationTestSettings>
    {
        return TryGetCurrentActivePage<TPage, AutomationTestSettings>(webDriver, settings);
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
