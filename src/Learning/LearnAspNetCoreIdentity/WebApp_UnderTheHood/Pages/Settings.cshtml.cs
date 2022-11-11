using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApp_UnderTheHood.Authorization;

namespace WebApp_UnderTheHood.Pages;

[Authorize(policy: AppAuthorizationPolicies.AdminOnly)]
public class SettingsModel : PageModel
{
    public void OnGet()
    {
    }
}
