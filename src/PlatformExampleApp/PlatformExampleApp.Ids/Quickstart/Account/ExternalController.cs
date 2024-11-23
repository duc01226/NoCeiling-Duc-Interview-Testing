using System.Security.Claims;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.Events;
using IdentityServer4.Services;
using IdentityServer4.Test;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace PlatformExampleApp.Ids.Quickstart.Account;

[SecurityHeaders]
[AllowAnonymous]
public class ExternalController : Controller
{
    private readonly IEventService events;
    private readonly IIdentityServerInteractionService interaction;
    private readonly ILogger<ExternalController> logger;
    private readonly TestUserStore users;

    public ExternalController(
        IIdentityServerInteractionService interaction,
        IEventService events,
        ILogger<ExternalController> logger,
        TestUserStore users = null)
    {
        // if the TestUserStore is not in DI, then we'll just use the global users collection
        // this is where you would plug in your own custom identity management library (e.g. ASP.NET Identity)
        this.users = users ?? new TestUserStore(TestUsers.Users);

        this.interaction = interaction;
        this.logger = logger;
        this.events = events;
    }

    /// <summary>
    /// initiate roundtrip to external authentication provider
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Challenge(string scheme, string returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
            returnUrl = "~/";

        // validate returnUrl - either it is a valid OIDC URL or back to a local page
        if (Url.IsLocalUrl(returnUrl) == false && interaction.IsValidReturnUrl(returnUrl) == false)
            // user might have clicked on a malicious link - should be logged
            throw new Exception("invalid return URL");

        // start challenge and roundtrip the return URL and scheme 
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback)),
            Items =
            {
                {
                    "returnUrl", returnUrl
                },
                {
                    "scheme", scheme
                }
            }
        };

        return Challenge(props, scheme);
    }

    /// <summary>
    /// Post processing of external authentication
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Callback()
    {
        // read external identity from the temporary cookie
        var result =
            await HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
        if (result?.Succeeded != true)
            throw new Exception("External authentication error");

        if (logger.IsEnabled(LogLevel.Debug))
        {
            var externalClaims = result.Principal.Claims.Select(c => $"{c.Type}: {c.Value}");
            logger.LogDebug("External claims: {Claims}", externalClaims);
        }

        // lookup our user and external provider info
        var (user, provider, providerUserId, claims) = FindUserFromExternalProvider(result);
        user ??= AutoProvisionUser(provider, providerUserId, claims);

        // this allows us to collect any additional claims or properties
        // for the specific protocols used and store them in the local auth cookie.
        // this is typically used to store data needed for signout from those protocols.
        var additionalLocalClaims = new List<Claim>();
        var localSignInProps = new AuthenticationProperties();
        ProcessLoginCallback(result, additionalLocalClaims, localSignInProps);

        // issue authentication cookie for user
        var isuser = new IdentityServerUser(user.SubjectId)
        {
            DisplayName = user.Username,
            IdentityProvider = provider,
            AdditionalClaims = additionalLocalClaims
        };

        await HttpContext.SignInAsync(isuser, localSignInProps);

        // delete temporary cookie used during external authentication
        await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

        // retrieve return URL
        var returnUrl = result.Properties.Items["returnUrl"] ?? "~/";

        // check if external login is in the context of an OIDC request
        var context = await interaction.GetAuthorizationContextAsync(returnUrl);
        await events.RaiseAsync(
            new UserLoginSuccessEvent(
                provider,
                providerUserId,
                user.SubjectId,
                user.Username,
                true,
                context?.Client.ClientId));

        if (context?.IsNativeClient() == true)
            // The client is native, so this change in how to
            // return the response is for better UX for the end user.
            return this.LoadingPage("Redirect", returnUrl);

        return Redirect(returnUrl);
    }

    private (TestUser user, string provider, string providerUserId, IEnumerable<Claim> claims)
        FindUserFromExternalProvider(AuthenticateResult result)
    {
        var externalUser = result.Principal;

        // try to determine the unique id of the external user (issued by the provider)
        // the most common claim type for that are the sub claim and the NameIdentifier
        // depending on the external provider, some other claim type might be used
        var userIdClaim = externalUser.FindFirst(JwtClaimTypes.Subject) ??
                          externalUser.FindFirst(ClaimTypes.NameIdentifier) ??
                          throw new Exception("Unknown userid");

        // remove the user id claim so we don't include it as an extra claim if/when we provision the user
        var claims = externalUser.Claims.ToList();
        claims.Remove(userIdClaim);

        var provider = result.Properties.Items["scheme"];
        var providerUserId = userIdClaim.Value;

        // find external user
        var user = users.FindByExternalProvider(provider, providerUserId);

        return (user, provider, providerUserId, claims);
    }

    private TestUser AutoProvisionUser(string provider, string providerUserId, IEnumerable<Claim> claims)
    {
        var user = users.AutoProvisionUser(provider, providerUserId, claims.ToList());
        return user;
    }

    // if the external login is OIDC-based, there are certain things we need to preserve to make logout work
    // this will be different for WS-Fed, SAML2p or other protocols
    private static void ProcessLoginCallback(
        AuthenticateResult externalResult,
        List<Claim> localClaims,
        AuthenticationProperties localSignInProps)
    {
        // if the external system sent a session id claim, copy it over
        // so we can use it for single sign-out
        var sid = externalResult.Principal!.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.SessionId);
        if (sid != null)
            localClaims.Add(new Claim(JwtClaimTypes.SessionId, sid.Value));

        // if the external provider issued an id_token, we'll keep it for signout
        var idToken = externalResult.Properties!.GetTokenValue("id_token");
        if (idToken != null)
        {
            localSignInProps.StoreTokens(
            [
                new AuthenticationToken
                {
                    Name = "id_token",
                    Value = idToken
                }
            ]);
        }
    }
}
