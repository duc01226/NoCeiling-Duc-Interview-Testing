using AspNetCoreIdentity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetCoreIdentity.Pages.Account;

public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<User> userManager;

    public ConfirmEmailModel(UserManager<User> userManager)
    {
        this.userManager = userManager;
    }

    [BindProperty]
    public string Message { get; set; } = "";

    [BindProperty]
    public bool ConfirmSucceeded { get; set; }

    public async Task<IActionResult> OnGetAsync(string userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId);

        if (user != null)
        {
            var confirmResult = await userManager.ConfirmEmailAsync(user, token);

            if (confirmResult.Succeeded)
            {
                Message = "Email address is successfully confirm. You can try to login now.";
                ConfirmSucceeded = true;
            }
        }
        else
        {
            Message = "Failed to validate email.";
        }

        return Page();
    }
}
