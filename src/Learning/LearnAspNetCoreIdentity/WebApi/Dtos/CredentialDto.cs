using WebApi.Dtos.Abstract;

namespace WebApi.Dtos;

public class CredentialDto : Dto
{
    public string? UserName { get; set; }

    public string? Password { get; set; }
}
