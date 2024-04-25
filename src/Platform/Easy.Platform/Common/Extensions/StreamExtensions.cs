using System.IO;
using System.Text;

namespace Easy.Platform.Common.Extensions;

public static class StreamExtensions
{
    public static async Task<byte[]> GetBinaries(this Stream stream)
    {
        await using (var memoryStream = new MemoryStream(stream.Length > int.MaxValue ? int.MaxValue : (int)stream.Length))
        {
            await stream.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }
    }

    public static async Task<string> ReadToEndAsString(this Stream stream, CancellationToken cancellationToken = default)
    {
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            return await reader.ReadToEndAsync(cancellationToken);
        }
    }
}
