using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApp_UnderTheHood.Authorization;

namespace WebApp_UnderTheHood.Pages;

[Authorize(Policy = AppAuthorizationPolicies.MustBelongToHrDepartment)]
public class HumanResourceModel : PageModel
{
    public void OnGet()
    {
    }
}
