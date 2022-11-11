using System.ComponentModel.DataAnnotations;
using AspNetCoreIdentity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetCoreIdentity.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<User> signInManager;

    public LoginModel(SignInManager<User> signInManager)
    {
        this.signInManager = signInManager;
    }

    [BindProperty]
    public CredentialViewModel Credential { get; set; } = new CredentialViewModel();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync([FromQuery] string? returnUrl)
    {
        if (ModelState.IsValid)
        {
            var signInResult = await signInManager.PasswordSignInAsync(
                userName: Credential.Email,
                password: Credential.Password,
                isPersistent: Credential.RememberMe,
                lockoutOnFailure: false);

            if (signInResult.Succeeded)
                return Redirect(returnUrl ?? "~/");
            if (signInResult.RequiresTwoFactor)
                return RedirectToPage(
                    "/Account/LoginTwoFactor",
                    new
                    {
                        Credential.Email,
                        Credential.RememberMe
                    });
            if (signInResult.IsLockedOut)
                ModelState.AddModelError("Login", "You are locked out.");
            else
                ModelState.AddModelError("Login", "Failed to login");
        }

        return Page();
    }
}

public class CredentialViewModel
{
    [Required]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
