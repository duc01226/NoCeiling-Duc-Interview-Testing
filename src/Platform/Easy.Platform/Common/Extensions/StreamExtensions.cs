using System.IO;
using System.Net.Http;

namespace Easy.Platform.Common.Extensions;

public static class StreamExtensions
{
    public static async Task<byte[]> GetBinaries(this Stream stream)
    {
        await using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }
    }
}

public static class HttpContentExtensions
{
    public static async Task<MemoryStream> GetMemoryStreamAsync(this HttpContent httpContent)
    {
        var stream = new MemoryStream();
        await httpContent.CopyToAsync(stream);
        stream.Position = 0;

        return stream;
    }
}
