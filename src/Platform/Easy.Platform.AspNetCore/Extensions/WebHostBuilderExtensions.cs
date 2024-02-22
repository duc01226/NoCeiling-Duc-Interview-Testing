using System.IO;
using Easy.Platform.Common;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Easy.Platform.AspNetCore.Extensions;

public static class WebHostBuilderExtensions
{
    /// <summary>
    /// Use the given https certificate for handling and trust https request
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <param name="httpsCertFileRelativePath">Relative path to entry executing assembly location</param>
    /// <param name="httpsCertPassword"></param>
    /// <param name="ignoreIfFileNotExisting"></param>
    /// <returns></returns>
    public static IWebHostBuilder UseCustomHttpsCert(
        this IWebHostBuilder hostBuilder,
        string httpsCertFileRelativePath,
        string httpsCertPassword,
        bool ignoreIfFileNotExisting = false)
    {
        var fullHttpsCertFilePath = Util.PathBuilder.GetFullPathByRelativeToEntryExecutionPath(httpsCertFileRelativePath);

        var isCertFileExisting = File.Exists(fullHttpsCertFilePath)
            .Ensure(
                must: isCertFileExisting => isCertFileExisting || ignoreIfFileNotExisting,
                $"HttpsCertFileRelativePath:[{httpsCertFileRelativePath}] to FullHttpsCertFilePath:[{fullHttpsCertFilePath}] does not exists");
        var listenUrls = PlatformEnvironment.AspCoreUrlsValue?.Split(";");

        return hostBuilder.PipeIf(
            listenUrls != null && isCertFileExisting,
            p => p.ConfigureKestrel(
                serverOptions =>
                {
                    listenUrls!.ForEach(
                        listenUrl =>
                        {
                            if (listenUrl.StartsWith("http://*:") || listenUrl.StartsWith("https://*:"))
                            {
                                var listenAnyPort = listenUrl
                                    .Replace("http://*:", "http://0.0.0.0:")
                                    .Replace("https://*:", "https://0.0.0.0:")
                                    .ToUri()
                                    .Port;

                                serverOptions.ListenAnyIP(
                                    listenAnyPort,
                                    listenOptions => ConfigUseHttps(listenOptions, listenUrl, fullHttpsCertFilePath!, httpsCertPassword));
                            }
                            else if (listenUrl.Contains("://localhost"))
                            {
                                serverOptions.ListenLocalhost(
                                    listenUrl.ToUri().Port,
                                    listenOptions => ConfigUseHttps(listenOptions, listenUrl, fullHttpsCertFilePath!, httpsCertPassword));
                            }
                            else
                            {
                                serverOptions.Listen(
                                    new UriEndPoint(listenUrl.ToUri()),
                                    listenOptions => ConfigUseHttps(listenOptions, listenUrl, fullHttpsCertFilePath!, httpsCertPassword));
                            }
                        });
                }));

        static void ConfigUseHttps(ListenOptions listenOptions, string listenUrl, string certFilePath, string certPassword)
        {
            listenOptions.PipeIf(listenUrl.StartsWith("https"), _ => _.UseHttps(fileName: certFilePath!, certPassword));
        }
    }
}
