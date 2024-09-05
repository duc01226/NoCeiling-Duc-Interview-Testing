using OpenQA.Selenium;

namespace Easy.Platform.AutomationTest;

public interface ILazyWebDriver : IDisposable
{
    IWebDriver Value { get; }
    bool Disposed { get; set; }
}

public class LazyWebDriver : IScopedLazyWebDriver, ISingletonLazyWebDriver
{
    public LazyWebDriver(AutomationTestSettings settings)
    {
        LazyDriver = new Lazy<IWebDriver>(valueFactory: () => WebDriverManager.New(settings).CreateWebDriver());
    }

    private Lazy<IWebDriver> LazyDriver { get; }

    public IWebDriver Value => LazyDriver.Value;
    public bool Disposed { get; set; }

    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(disposing: true);

        // Suppress finalization.
        GC.SuppressFinalize(obj: this);
    }

    ~LazyWebDriver()
    {
        Dispose(false);
    }

    // Protected implementation of Dispose pattern.
    protected virtual void Dispose(bool disposing)
    {
        if (!Disposed)
        {
            // Release managed resources
            if (disposing)
                LazyDriver.PipeAction(
                    _ =>
                    {
                        if (LazyDriver.IsValueCreated)
                        {
                            Value.Quit();
                            Value.Dispose();
                        }
                    });

            // Release unmanaged resources

            Disposed = true;
        }
    }
}

public interface IScopedLazyWebDriver : ILazyWebDriver
{
}

public interface ISingletonLazyWebDriver : ILazyWebDriver
{
}
