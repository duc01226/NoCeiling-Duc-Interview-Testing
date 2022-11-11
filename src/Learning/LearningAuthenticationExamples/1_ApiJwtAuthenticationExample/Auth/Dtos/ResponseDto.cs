using ApiJwtAuthenticationExample.Auth.Dtos.Abstract;

namespace ApiJwtAuthenticationExample.Auth.Dtos;

public class ResponseDto : Dto
{
    public enum Statuses
    {
        Error,
        Success
    }

    public Statuses Status { get; set; }
    public string? Message { get; set; }

    public static ResponseDto Create(Statuses status, string? message = null)
    {
        return new ResponseDto
        {
            Status = status,
            Message = message
        };
    }
}
