using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using AspNetCoreIdentity.Entities;
using AspNetCoreIdentity.Infrastructures;
using AspNetCoreIdentity.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AspNetCoreIdentity.Pages.Account;

public class LoginTwoFactorModel : PageModel
{
    private readonly IEmailService emailService;
    private readonly SignInManager<User> signinManager;
    private readonly IOptions<SmtpSetting> smtpSetting;
    private readonly UserManager<User> userManager;

    public LoginTwoFactorModel(
        UserManager<User> userManager,
        IEmailService emailService,
        IOptions<SmtpSetting> smtpSetting,
        SignInManager<User> signinManager)
    {
        this.userManager = userManager;
        this.emailService = emailService;
        this.smtpSetting = smtpSetting;
        this.signinManager = signinManager;
    }

    [BindProperty]
    public LoginTwoFactorViewModel Vm { get; set; } = new LoginTwoFactorViewModel();

    public async Task<IActionResult> OnGetAsync(string email, bool rememberMe)
    {
        var (securityCode, isSendMailSuccess, errors) = await Generate2FaTokenAndSendEmail(email);

        if (isSendMailSuccess == false)
            Vm.FallbackSendEmailDisplaySecurityCode =
                securityCode; // Send email may not work for learning purpose or can't find a  email provider
        if (errors?.Any() == true)
            errors.ForEach(error => ModelState.TryAddModelError(error.Key, error.Value));
        Vm.VerifyForm.RememberMe = rememberMe;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await signinManager.TwoFactorSignInAsync(
            TokenOptions.DefaultEmailProvider,
            Vm.VerifyForm.MfaToken,
            Vm.VerifyForm.RememberMe,
            rememberClient: false);

        if (result.Succeeded)
        {
            return RedirectToPage("/Index");
        }

        if (result.IsLockedOut)
            ModelState.AddModelError("Login2FA", "You are locked out.");
        else
            ModelState.AddModelError("Login2FA", "Failed to login.");

        return Page();
    }

    private async Task<(string?, bool?, List<KeyValuePair<string, string>>?)> Generate2FaTokenAndSendEmail(
        string email)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user != null)
        {
            var securityCode = await GenerateTwoFactorTokenAsync();

            var isSendMailSuccess = await TrySendSecurityCodeToEmail(securityCode, email);

            return (securityCode, isSendMailSuccess, null);
        }

        return (
            null,
            null,
            new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("UserNotFound", $"No user found for the email: {email}")
            });

        async Task<string> GenerateTwoFactorTokenAsync()
        {
            var securityCode = await userManager.GenerateTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultEmailProvider);
            if (securityCode == null)
                throw new Exception("Something went wrong. Can't generate the security code.");
            return securityCode;
        }

        async Task<bool> TrySendSecurityCodeToEmail(string securityCode, string email)
        {
            try
            {
                // Send to the user email
                await emailService.SendAsync(
                    new MailMessage(
                        from: smtpSetting.Value.User,
                        to: email,
                        "My web app OTP",
                        body: $"Please use this code as the OTP: {securityCode}"));

                return true;
            }
            catch // Send email may not work for learning purpose or can't find a  email provider
            {
                return false;
            }
        }
    }
}

public class LoginTwoFactorViewModel
{
    public string? FallbackSendEmailDisplaySecurityCode { get; set; }

    public VerifyFormViewModel VerifyForm { get; set; } = new VerifyFormViewModel();

    public class VerifyFormViewModel
    {
        [Required]
        [Display(Name = "Security Token")]
        public string MfaToken { get; set; } = "";

        public bool RememberMe { get; set; }
    }
}
