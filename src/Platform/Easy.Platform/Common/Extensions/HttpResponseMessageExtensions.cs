using System.Net.Http;

namespace Easy.Platform.Common.Extensions;

public static class HttpResponseMessageExtensions
{
    public static HttpResponseMessage EnsureSuccessStatusCodeWithErrorContent(this HttpResponseMessage httpResponseMessage)
    {
        try
        {
            httpResponseMessage.EnsureSuccessStatusCode();

            return httpResponseMessage;
        }
        catch (Exception e)
        {
            throw new Exception(
                $"A http request has been failed. Request Url: {httpResponseMessage.RequestMessage?.RequestUri?.AbsoluteUri ?? "n/a"}. Response: {httpResponseMessage.Content.ReadAsStringAsync().GetResult() ?? "n/a"}",
                e);
        }
    }
}
