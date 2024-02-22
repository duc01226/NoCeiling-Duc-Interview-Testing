namespace Easy.Platform.AutomationTest;

/// <summary>
/// Default AutomationTestSettings for the framework. You could define class extend from this AutomationTestSettings.
/// It will be auto registered via IConfiguration by default or you could override <see cref="BaseStartup.AutomationTestSettingsProvider" /> to register
/// by yourself
/// </summary>
public class AutomationTestSettings
{
    private const int DefaultPageLoadTimeoutSeconds = 300;

    public Dictionary<string, string> AppNameToOrigin { get; set; } = [];
    public bool UseRemoteWebDriver { get; set; }
    public string? RemoteWebDriverUrl { get; set; }
    public WebDriverTypes WebDriverType { get; set; }
    public int? PageLoadTimeoutSeconds { get; set; } = DefaultPageLoadTimeoutSeconds;
    public double RemoteWebDriverCommandTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Arguments for web driver, eliminated by ";". Example: "no-sandbox;headless;window-size=1920,1080"
    /// </summary>
    public string? WebDriverOptionsArguments { get; set; }

    public string[] GetWebDriverConfigArgumentsList()
    {
        return WebDriverOptionsArguments?.Split(";").Where(p => !p.IsNullOrWhiteSpace()).Select(p => p.Trim()).ToArray() ?? [];
    }

    public enum WebDriverTypes
    {
        Chrome,
        Firefox,
        Edge
    }
}
