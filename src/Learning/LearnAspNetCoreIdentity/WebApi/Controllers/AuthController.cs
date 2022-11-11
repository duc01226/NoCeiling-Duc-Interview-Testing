using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using WebApi.Auth;
using WebApi.Authorization.Dtos;
using WebApi.Dtos;

namespace WebApi.Controllers;

[Route("[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration configuration;

    public AuthController(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    [HttpPost]
    public IActionResult Login([FromBody] CredentialDto credential)
    {
        // verify the credentials
        if (ModelState.IsValid && credential.UserName == "admin" && credential.Password == "password")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, credential.UserName),
                new Claim(ClaimTypes.Email, $"{credential.UserName}@mywebsite.com"),
                new Claim(AppAuthenticationClaims.HrDepartment.Type, AppAuthenticationClaims.HrDepartment.Value),
                new Claim(AppAuthenticationClaims.Admin.Type, AppAuthenticationClaims.Admin.Value),
                new Claim(AppAuthenticationClaims.Manager.Type, AppAuthenticationClaims.Manager.Value),
                new Claim(AppAuthenticationClaims.EmploymentDate.Type, new DateTime(2021, 1, 1).ToString())
            };

            var expiresAt = DateTime.UtcNow.AddMinutes(1);

            return Ok(new JwtTokenResponseDto
            {
                AccessToken = GenerateToken(claims, expiresAt),
                ExpiresAt = expiresAt
            });
        }

        return Unauthorized();
    }

    private string GenerateToken(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        var jwtToken = new JwtSecurityToken(
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                key: new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:SecretAuthenticationKey"])),
                algorithm: SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token: jwtToken);
    }
}
