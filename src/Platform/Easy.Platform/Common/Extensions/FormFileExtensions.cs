using System.IO;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.Common.Extensions;

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

    public static async Task<T> GetStreamReader<T>(this IFormFile formFile, Func<StreamReader, Task<T>> handle)
    {
        await using (var fileStream = new MemoryStream())
        {
            await formFile.CopyToAsync(fileStream);
            fileStream.Position = 0;

            return await handle(new StreamReader(fileStream));
        }
    }

    public static string GetFileExtension(this IFormFile file)
    {
        return Path.GetExtension(file.FileName);
    }
}
