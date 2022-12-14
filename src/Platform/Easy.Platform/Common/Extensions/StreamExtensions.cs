using System.IO;

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
