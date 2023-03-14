namespace Easy.Platform.AutomationTest;

/// <summary>
/// Default AutomationTestSettings for the framework. You could define class extend from this AutomationTestSettings.
/// It will be auto registered via IConfiguration by default or you could override <see cref="BaseStartup.AutomationTestSettingsProvider"/> to register
/// by yourself
/// </summary>
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
