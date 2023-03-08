namespace Easy.Platform.AutomationTest;

public class AutomationTestSettings
{
    public Dictionary<string, string> AppNameToOrigin { get; set; } = new();
    public bool UseRemoteWebDriver { get; set; }
    public string? RemoteWebDriverUrl { get; set; }
    public WebDriverTypes WebDriverType { get; set; }

    public enum WebDriverTypes
    {
        Chrome,
        Firefox,
        Edge
    }
}
