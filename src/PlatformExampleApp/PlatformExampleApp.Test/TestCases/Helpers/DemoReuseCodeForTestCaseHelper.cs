using PlatformExampleApp.Test.Apps.TextSnippet.Pages;

namespace PlatformExampleApp.Test.TestCases.Helpers;

public static class DemoReuseCodeForTestCaseHelper
{
    public static TextSnippetApp.HomePage GetLoadingDataFinishedWithFullPagingDataHomePage(
        this IWebDriver webDriver,
        TestSettings settings)
    {
        return webDriver.NavigatePage<TextSnippetApp.HomePage>(settings)
            .WaitInitLoadingDataSuccessWithFullPagingData();
    }
}
