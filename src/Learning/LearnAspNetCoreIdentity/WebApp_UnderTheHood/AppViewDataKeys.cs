using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace WebApp_UnderTheHood;

public static class AppViewDataExtensions
{
    public const string IsLoginPageKey = "IsLoginPage";


    public static bool IsLoginPage(this ViewDataDictionary viewData)
    {
        return viewData[IsLoginPageKey] as bool? ?? false;
    }

    public static bool SetIsLoginPage(this ViewDataDictionary viewData, bool value)
    {
        viewData[IsLoginPageKey] = value;
        return value;
    }
}
