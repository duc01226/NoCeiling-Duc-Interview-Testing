using System.ComponentModel.DataAnnotations;
using ApiJwtAuthenticationExample.Auth.Dtos.Abstract;
using Microsoft.AspNetCore.Identity;

namespace ApiJwtAuthenticationExample.Auth.Dtos;

public class RegisterDto : Dto
{
    [Required(ErrorMessage = "User Name is required")]
    public string? Username { get; set; }

    [EmailAddress]
    [Required(ErrorMessage = "Email is required")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required")]
    public string? Password { get; set; }

    public IdentityUser ToNewUser()
    {
        return new IdentityUser
        {
            Email = Email,
            UserName = Username
        };
    }
}
