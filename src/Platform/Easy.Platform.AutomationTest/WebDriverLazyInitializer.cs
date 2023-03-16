using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest;

public sealed class WebDriverLazyInitializer : IDisposable
{
    public WebDriverLazyInitializer(AutomationTestSettings settings)
    {
        LazyDriver = new Lazy<IWebDriver>(valueFactory: () => WebDriverManager.New(settings).CreateWebDriver());
    }


    public IWebDriver Value => LazyDriver.Value;

    private Lazy<IWebDriver> LazyDriver { get; }

    public void Dispose()
    {
        if (LazyDriver.IsValueCreated)
        {
            Value.Quit();
            Value.Dispose();
        }
    }
}
