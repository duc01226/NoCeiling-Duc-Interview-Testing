#nullable enable
using System.Net;
using System.Web;

namespace Easy.Platform.Common.Extensions;

public static class UrlExtension
{
    public const int DefaultHttpPort = 80;
    public const int DefaultHttpsPort = 443;

    /// <summary>
    /// Converts a string to a Uri.
    /// </summary>
    /// <param name="url">The string to convert.</param>
    /// <returns>The Uri created from the string.</returns>
    public static Uri ToUri(this string url, params ValueTuple<string, object?>[] queryParams)
    {
        return new UriBuilder(url)
            .PipeIf(
                queryParams.Any(),
                uriBuilder =>
                    uriBuilder.With(
                        p => p.Query = HttpUtility.ParseQueryString(string.Empty)
                            .PipeAction(queryCollection => queryParams.ForEach(queryParam => queryCollection[queryParam.Item1] = queryParam.Item2?.ToString()))
                            .ToString()))
            .Uri;
    }

    /// <summary>
    /// Tries to parse a string to a Uri.
    /// </summary>
    /// <param name="str">The string to parse.</param>
    /// <returns>The Uri if the string can be parsed, null otherwise.</returns>
    public static Uri? TryParseUri(this string str)
    {
        try
        {
            return str.ToUri();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the origin of a Uri.
    /// </summary>
    /// <param name="url">The Uri to get the origin from.</param>
    /// <returns>The origin of the Uri.</returns>
    public static string Origin(this Uri url)
    {
        return $"{url.Scheme}://{url.Host}".PipeIf(url.Port is not DefaultHttpPort and not DefaultHttpsPort, s => $"{s}:{url.Port}");
    }

    /// <summary>
    /// Concatenates a relative path to a Uri.
    /// </summary>
    /// <param name="uri">The Uri to concatenate the relative path to.</param>
    /// <param name="relativePath">The relative path to concatenate.</param>
    /// <returns>The Uri with the concatenated relative path.</returns>
    public static Uri ConcatRelativePath(this Uri uri, string relativePath)
    {
        return new UriBuilder(uri)
            .With(builder => builder.Path = builder.Path.TrimEnd('/') + "/" + relativePath.TrimStart('/'))
            .Uri;
    }

    /// <summary>
    /// Gets the query parameters of a Uri.
    /// </summary>
    /// <param name="url">The Uri to get the query parameters from.</param>
    /// <returns>A dictionary of the query parameters.</returns>
    public static Dictionary<string, string?> QueryParams(this Uri url)
    {
        return url.Query.PipeIfOrDefault(
            queryStr => !queryStr.IsNullOrEmpty(),
            queryStr => HttpUtility.ParseQueryString(queryStr).Pipe(queryNvc => queryNvc.AllKeys.ToDictionary(k => k!, k => queryNvc[k])),
            []);
    }

    /// <summary>
    /// Converts a dictionary of query parameters to a query string.
    /// </summary>
    /// <param name="queryParams">The dictionary of query parameters.</param>
    /// <returns>The query string.</returns>
    public static string ToQueryString(this Dictionary<string, string> queryParams)
    {
        return $"?{queryParams.Select(keyValuePair => WebUtility.UrlEncode($"{keyValuePair.Key}={keyValuePair.Value}")).JoinToString('&')}";
    }

    /// <summary>
    /// Gets the path of a Uri.
    /// </summary>
    /// <param name="uri">The Uri to get the path from.</param>
    /// <returns>The path of the Uri.</returns>
    public static string Path(this Uri uri)
    {
        return uri.PathAndQuery.Substring(0, uri.PathAndQuery.Length - uri.Query.Length).TrimEnd('/');
    }
}
