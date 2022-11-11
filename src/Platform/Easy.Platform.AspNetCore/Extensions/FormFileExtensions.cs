using System.IO;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.AspNetCore.Extensions;

public static class FormFileExtensions
{
    public static async Task<byte[]> GetFileBinaries(this IFormFile formFile)
    {
        await using (var fileStream = new MemoryStream())
        {
            await formFile.CopyToAsync(fileStream);

            return fileStream.ToArray();
        }
    }
}
