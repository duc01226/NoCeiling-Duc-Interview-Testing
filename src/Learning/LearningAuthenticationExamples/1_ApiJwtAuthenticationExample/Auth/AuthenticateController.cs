using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiJwtAuthenticationExample.Auth.Const;
using ApiJwtAuthenticationExample.Auth.Dtos;
using ApiJwtAuthenticationExample.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ApiJwtAuthenticationExample.Auth;

[Route("api/[controller]")]
[ApiController]
public class AuthenticateController : Controller
{
    private readonly IConfiguration configuration;
    private readonly RoleManager<IdentityRole> roleManager;
    private readonly UserManager<IdentityUser> userManager;

    public AuthenticateController(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        this.userManager = userManager;
        this.roleManager = roleManager;
        this.configuration = configuration;
    }

    [HttpPost]
    [Route("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        var user = await userManager.FindByNameAsync(request.Username);

        if (user != null && await userManager.CheckPasswordAsync(user, request.Password))
        {
            var userRoles = await userManager.GetRolesAsync(user);

            var authClaims = BuildAuthClaims(user, userRoles);

            var token = BuildToken(authClaims);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo
            });
        }

        return Unauthorized();

        static List<Claim> BuildAuthClaims(IdentityUser user, IList<string> userRoles)
        {
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            authClaims.AddRange(userRoles.Select(userRole => new Claim(ClaimTypes.Role, userRole)));

            return authClaims;
        }
    }

    [HttpPost]
    [Route("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto request)
    {
        return await Register(request, withRoles: new List<string>
        {
            UserRoles.User
        });
    }

    [HttpPost]
    [Route("register-admin")]
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterDto request)
    {
        return await Register(request, withRoles: new List<string>
        {
            UserRoles.Admin,
            UserRoles.User
        });
    }

    private async Task<IActionResult> Register(RegisterDto request, List<string>? withRoles)
    {
        var existingUser = await userManager.FindByNameAsync(request.Username);

        if (existingUser != null)
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ResponseDto.Create(status: ResponseDto.Statuses.Error, message: "User already exists!"));
        return await CreateNewUser(request, withRoles);

        async Task<IActionResult> CreateNewUser(RegisterDto registerDto, List<string>? withRoles)
        {
            var newUser = registerDto.ToNewUser()
                .With(p => p.SecurityStamp = Guid.NewGuid().ToString());

            var createNewUserResult = await userManager.CreateAsync(newUser, registerDto.Password);

            if (createNewUserResult.Succeeded)
            {
                if (withRoles != null)
                    await AddRolesToUser(user: newUser, roles: withRoles);

                return Ok(ResponseDto.Create(
                    status: ResponseDto.Statuses.Success,
                    message: "User created successfully!"));
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ResponseDto.Create(
                    status: ResponseDto.Statuses.Error,
                    message: "User creation failed! Please check user details and try again."));

            async Task AddRolesToUser(IdentityUser user, List<string> roles)
            {
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                        await roleManager.CreateAsync(new IdentityRole(role));

                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }

    private JwtSecurityToken BuildToken(List<Claim> authClaims)
    {
        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]));

        // What is JWT and Why Should You Use JWT: https://youtu.be/7Q17ubqLfaM
        // Audience: What is the target of this token. In other words which services, apis, products should accept this token an access token for the service. They may be many valid tokens in the world, but not all of those tokens have been granted by the user (or resource owner) to allow access to the resources saved in the product services. A token valid for Google drive should not be accepted for GMail, even if both of them have the same issuer, they’ll have different audiences. Why? Because a user may have given access to a 3rd party service to a access their GMail, but not their documents in Drive.
        // Issuer: Who created the token. This can be verified by using the well-known openid configuration endpoint and the public keys that are listed there. Since issuers are tied to DNS entries/url paths, each issuer must be unique. Two services can’t both be the same issuer. Tokens issued by Google will have a different issuer than the ones issued by Authress.
        // JSON Web Token Claims: JSON web tokens (JWTs) claims are pieces of information asserted about a subject. For example, an ID token (which is always a JWT) can contain a claim called name that asserts that the name of the user authenticating is "John Doe".
        // Reserved Claims: Claims defined by the JWT specification to ensure interoperability with third-party, or external, applications. OIDC standard claims are reserved claims. Ex: iss (issuer): Issuer of the JWT; sub (subject): Subject of the JWT (the user); aud (audience): Recipient for which the JWT is intended; exp (expiration time): Time after which the JWT expires; nbf (not before time): Time before which the JWT must not be accepted for processing; iat (issued at time): Time at which the JWT was issued; can be used to determine age of the JWT; jti (JWT ID): Unique identifier; can be used to prevent the JWT from being replayed (allows a token to be used only once);
        // Custom Claims: Claims that you define yourself. Name these claims carefully, such as through namespacing (which Auth0 requires), to avoid collision with reserved claims or other custom claims. It can be challenging to deal with two claims of the same name that contain differing information.
        var token = new JwtSecurityToken(
            issuer: configuration["JWT:ValidIssuer"],
            audience: configuration["JWT:ValidAudience"],
            expires: DateTime.Now.AddHours(3),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        return token;
    }
}
