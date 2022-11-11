using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetCoreIdentity.Pages;

[Authorize]
public class IndexModel : PageModel
{
    public IndexModel(ILogger<IndexModel> logger)
    {
    }

    public void OnGet()
    {
    }
}
