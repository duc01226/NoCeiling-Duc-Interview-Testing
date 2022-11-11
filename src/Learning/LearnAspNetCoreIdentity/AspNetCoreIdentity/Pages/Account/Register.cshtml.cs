using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Security.Claims;
using AspNetCoreIdentity.Entities;
using AspNetCoreIdentity.Infrastructures;
using AspNetCoreIdentity.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AspNetCoreIdentity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly IEmailService emailService;
    private readonly IOptions<SmtpSetting> smtpSetting;
    private readonly UserManager<User> userManager;

    public RegisterModel(UserManager<User> userManager, IOptions<SmtpSetting> smtpSetting, IEmailService emailService)
    {
        this.userManager = userManager;
        this.smtpSetting = smtpSetting;
        this.emailService = emailService;
    }

    [BindProperty]
    public RegisterViewModel RegisterViewModel { get; set; } = new RegisterViewModel();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            // Validating Email address (Optional because User.RequireUniqueEmail has been configured)


            // Create the user
            var user = new User
            {
                Email = RegisterViewModel.Email,
                UserName = RegisterViewModel.Email,
                Department = RegisterViewModel.Department!,
                Position = RegisterViewModel.Position!,
                TwoFactorEnabled = RegisterViewModel.RequireTwoFactorAuth
            };

            var createUserResult = await userManager.CreateAsync(user, RegisterViewModel.Password);

            if (createUserResult.Succeeded)
            {
                // DEMO ANOTHER WAY TO ADD MORE USER INFO IS ADD USER CLAIM INSTEAD OF DEFINE CUSTOM USER ENTITY AND ADD DATA INTO USER TABLE
                // In reality, the identity claims like identity card, so not all information of a user should be stored in claims.
                // May be simple and important user information is enough. Or you could save all information into claims but
                // can filter to select some claims to save into identity jwt token to make it not too big
                await userManager.AddClaimAsync(user, new Claim(nameof(user.Department), RegisterViewModel.Department!));
                await userManager.AddClaimAsync(user, new Claim(nameof(user.Position), RegisterViewModel.Position!));

                // HOW: Token with user id (may be more like some timestamp, ...) will be sent to the email. When server validate the token,
                // It use the user id and the same information (secret key, hashing algorithm) that generate the token
                // to generate another token and compare it
                // In this case the token also is not stored in the db. It will be recreated when validate token
                var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

                var confirmationLink = Url.PageLink(
                    pageName: "/Account/ConfirmEmail",
                    values: new
                    {
                        userId = user.Id,
                        token = confirmationToken
                    })!;

                var confirmEmailMessage = new MailMessage(
                    from: smtpSetting.Value.User, // The sender
                    to: user.Email!,
                    subject: "Please confirm your email",
                    body: $"Please click on this link to confirm your email address: {confirmationLink}");

                try
                {
                    await emailService.SendAsync(confirmEmailMessage);
                }
                catch (Exception ex)
                {
                    // Support auto confirm if can not send email. only for testing if you could find an email provider
                    return Redirect(confirmationLink);
                }

                return RedirectToPage("/Account/Login");
            }

            createUserResult.Errors.ToList().ForEach(error =>
            {
                // The name "Register" doesn't matter because we are not associated with any model field
                // Need to set asp-validation-summary="All" to display this errors (Not Just ModelOnly)
                ModelState.AddModelError("Register", error.Description);
            });
        }

        return Page();
    }
}

public class RegisterViewModel
{
    [Required]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Required]
    [Display(Name = "Department")]
    public string? Department { get; set; }

    [Required]
    [Display(Name = "Position")]
    public string? Position { get; set; }

    [Required]
    [Display(Name = "Require Two Factor")]
    public bool RequireTwoFactorAuth { get; set; }
}
