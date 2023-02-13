using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest;

/// <summary>
/// This web driver is only created once during all test cases. Could reuse this if you want to save init new web driver resource. <br />
/// Should only use this for test cases which is not affected by other test cases if it's running in the same browser
/// </summary>
public class GlobalWebDriver : IDisposable
{
    public GlobalWebDriver(TestSettings settings)
    {
        LazyDriver = new Lazy<IWebDriver>(() => WebDriverManager.New(settings).CreateWebDriver());
    }

    public IWebDriver Value => LazyDriver.Value;
    public bool Disposed { get; set; }

    private Lazy<IWebDriver> LazyDriver { get; }

    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);

        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    // Protected implementation of Dispose pattern.
    protected virtual void Dispose(bool disposing)
    {
        if (Disposed)
            return;

        if (disposing && LazyDriver.IsValueCreated)
        {
            Value.Quit();
            Value.Dispose();
        }

        Disposed = true;
    }
}
