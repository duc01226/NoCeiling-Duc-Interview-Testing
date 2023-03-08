using PlatformExampleApp.Test.Apps.TextSnippet.Pages;

namespace PlatformExampleApp.Test.TestCases.Helpers;

public static class DemoReuseCodeForTestCaseHelper
{
    public static TextSnippetApp.HomePage GetLoadingDataFinishedWithFullPagingDataHomePage(
        this IWebDriver webDriver,
        AutomationTestSettings settings)
    {
        return webDriver.NavigatePage<TextSnippetApp.HomePage>(settings)
            .WaitInitLoadingDataSuccessWithFullPagingData();
    }
}
