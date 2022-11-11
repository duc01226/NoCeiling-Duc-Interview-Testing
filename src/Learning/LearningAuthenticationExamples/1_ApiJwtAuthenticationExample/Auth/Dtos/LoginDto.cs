using System.ComponentModel.DataAnnotations;
using ApiJwtAuthenticationExample.Auth.Dtos.Abstract;

namespace ApiJwtAuthenticationExample.Auth.Dtos;

public class LoginDto : Dto
{
    [Required(ErrorMessage = "User Name is required")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "Password is required")]
    public string? Password { get; set; }
}
