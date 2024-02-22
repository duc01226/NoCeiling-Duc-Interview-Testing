using System.IO;
using System.Net.Http;

namespace Easy.Platform.Common.Extensions;

public static class HttpContentExtensions
{
    /// <summary>
    /// Asynchronously reads the bytes from the current stream and writes them to a MemoryStream.
    /// </summary>
    /// <param name="httpContent">The HttpContent instance on which this extension method operates.</param>
    /// <returns>A task that represents the asynchronous copy operation. The value of the TResult parameter contains the MemoryStream where the contents of the current stream have been written.</returns>
    public static async Task<MemoryStream> GetMemoryStreamAsync(this HttpContent httpContent)
    {
        var stream = new MemoryStream();
        await httpContent.CopyToAsync(stream);
        stream.Position = 0;

        return stream;
    }
}
