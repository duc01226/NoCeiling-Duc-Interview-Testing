namespace Easy.Platform.AutomationTest;

public class AutomationTestSettings
{
    private const int DefaultPageLoadTimeoutSeconds = 300;

    public Dictionary<string, string> AppNameToOrigin { get; set; } = new();
    public bool UseRemoteWebDriver { get; set; }
    public string? RemoteWebDriverUrl { get; set; }
    public WebDriverTypes WebDriverType { get; set; }
    public int? PageLoadTimeoutSeconds { get; set; } = DefaultPageLoadTimeoutSeconds;

    public enum WebDriverTypes
    {
        Chrome,
        Firefox,
        Edge
    }
}
