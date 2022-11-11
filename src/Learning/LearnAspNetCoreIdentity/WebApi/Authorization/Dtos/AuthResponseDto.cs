using WebApi.Dtos.Abstract;

namespace WebApi.Authorization.Dtos;

public class JwtTokenResponseDto : Dto
{
    public string AccessToken { get; set; } = "";

    public DateTime ExpiresAt { get; set; }
}
