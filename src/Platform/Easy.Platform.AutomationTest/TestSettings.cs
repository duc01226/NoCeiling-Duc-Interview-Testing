namespace Easy.Platform.AutomationTest;

public class TestSettings
{
    public enum WebDriverTypes
    {
        Chrome,
        Firefox,
        Edge
    }

    public Dictionary<string, string> AppNameToOrigin { get; set; } = new();

    public bool UseRemoteWebDriver { get; set; }
    public string? RemoteWebDriverUrl { get; set; }
    public WebDriverTypes WebDriverType { get; set; }
}
