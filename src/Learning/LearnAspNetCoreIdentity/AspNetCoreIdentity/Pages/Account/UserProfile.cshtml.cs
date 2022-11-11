using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AspNetCoreIdentity.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AspNetCoreIdentity.Pages.Account;

[Authorize]
public class UserProfileModel : PageModel
{
    private readonly UserManager<User> userManager;

    public UserProfileModel(UserManager<User> userManager)
    {
        this.userManager = userManager;
    }

    [BindProperty]
    public UserProfileViewModel UserProfile { get; set; } = new UserProfileViewModel();

    [BindProperty]
    public string? SaveProfileSuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var (user, departmentClaim, positionClaim) = await GetUserInfoAsync();

        UserProfile.Email =
            User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value ??
            User.Identity!.Name!; // Demo get email from basic standard claims or from user identity name, this case email = username
        UserProfile.Department = departmentClaim?.Value;
        UserProfile.Position = positionClaim?.Value;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
            try
            {
                var (user, departmentClaim, positionClaim) = await GetUserInfoAsync();

                // Demo Save user claim
                await userManager.ReplaceClaimAsync(user, departmentClaim,
                    new Claim(nameof(Entities.User.Department), UserProfile.Department!));
                await userManager.ReplaceClaimAsync(user, positionClaim, new Claim(nameof(Entities.User.Position), UserProfile.Position!));

                // Demo save user entity
                //user.Department = UserProfile.Department!;
                //user.Position = UserProfile.Position!;

                await userManager.UpdateAsync(
                    user
                        .WithDepartment(UserProfile.Department!) // Demo build chain update function to make code better
                        .WithPosition(UserProfile.Position!));
            }
            catch (Exception e)
            {
                ModelState.AddModelError("UserProfile", $"Error occurred when saving user profile. {e.Message}");
            }

        SaveProfileSuccessMessage = "The user profile is saved successfully";
        return Page();
    }

    private async Task<(User, Claim?, Claim?)> GetUserInfoAsync()
    {
        var user = await userManager.FindByNameAsync(User.Identity!.Name);

        var claims = await userManager.GetClaimsAsync(user);

        var departmentClaim = claims.FirstOrDefault(x => x.Type == nameof(Entities.User.Department));
        var positionClaim = claims.FirstOrDefault(x => x.Type == nameof(Entities.User.Position));

        return (user, departmentClaim, positionClaim);
    }
}

public class UserProfileViewModel
{
    public string Email { get; set; } = "";

    [Required]
    public string? Department { get; set; }

    [Required]
    public string? Position { get; set; }
}
