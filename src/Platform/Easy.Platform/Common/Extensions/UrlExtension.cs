using System.Net;
using System.Web;

namespace Easy.Platform.Common.Extensions;

public static class UrlExtension
{
    public const int DefaultHttpPort = 80;
    public const int DefaultHttpsPort = 443;

    public static Uri ToUri(this string url)
    {
        return new Uri(url);
    }

    public static string Origin(this Uri url)
    {
        return $"{url.Scheme}://{url.Host}".PipeIf(url.Port != DefaultHttpPort && url.Port != DefaultHttpsPort, _ => $"{_}:{url.Port}");
    }

    public static Dictionary<string, string> QueryParams(this Uri url)
    {
        return url.Query.PipeIfOrDefault(
            queryStr => !queryStr.IsNullOrEmpty(),
            queryStr => HttpUtility.ParseQueryString(queryStr).Pipe(queryNvc => queryNvc.AllKeys.ToDictionary(k => k!, k => queryNvc[k])),
            new Dictionary<string, string>());
    }

    public static string ToQueryString(this Dictionary<string, string> queryParams)
    {
        return $"?{queryParams.Select(keyValuePair => WebUtility.UrlEncode($"{keyValuePair.Key}={keyValuePair.Value}")).JoinToString('&')}";
    }
}
