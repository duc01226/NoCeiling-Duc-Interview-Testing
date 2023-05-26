using System.Net;

namespace Easy.Platform.AspNetCore.ExceptionHandling;

public sealed class PlatformAspNetMvcErrorResponse
{
    public PlatformAspNetMvcErrorResponse(
        PlatformAspNetMvcErrorInfo error,
        HttpStatusCode statusCode,
        string requestId)
    {
        Error = error;
        StatusCode = (int)statusCode;
        RequestId = requestId;
    }

    public PlatformAspNetMvcErrorInfo Error { get; }

    public int StatusCode { get; }

    public string RequestId { get; set; }
}
