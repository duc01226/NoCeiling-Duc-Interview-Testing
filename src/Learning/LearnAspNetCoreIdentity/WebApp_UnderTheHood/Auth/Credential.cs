namespace WebApp_UnderTheHood.Auth;

public class Credential
{
    [Required]
    [Display(Name = "User name")]
    public string? UserName { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Remember me (Still keep cookie session after user close the browser. The cookie IsPersistent = true)")]
    public bool RememberMe { get; set; }
}
