using System.IO;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.Common.Extensions;

public static class FormFileExtensions
{
    /// <summary>
    /// Asynchronously gets the binary data of the specified form file.
    /// </summary>
    /// <param name="formFile">The form file to read.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the binary data of the file.</returns>
    public static async Task<byte[]> GetFileBinaries(this IFormFile formFile)
    {
        await using (var fileStream = new MemoryStream(formFile.Length > int.MaxValue ? int.MaxValue : (int)formFile.Length))
        {
            await formFile.CopyToAsync(fileStream);

            return fileStream.ToArray();
        }
    }

    /// <summary>
    /// Asynchronously handles the specified form file using a StreamReader.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="formFile">The form file to handle.</param>
    /// <param name="handle">A function to handle the StreamReader.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the result of the handle function.</returns>
    public static async Task<T> GetStreamReader<T>(this IFormFile formFile, Func<StreamReader, Task<T>> handle)
    {
        await using (var fileStream = new MemoryStream(formFile.Length > int.MaxValue ? int.MaxValue : (int)formFile.Length))
        {
            await formFile.CopyToAsync(fileStream);
            fileStream.Position = 0;

            return await handle(new StreamReader(fileStream));
        }
    }

    /// <summary>
    /// Gets the file extension of the specified form file.
    /// </summary>
    /// <param name="file">The form file to get the extension of.</param>
    /// <returns>The file extension of the form file.</returns>
    public static string GetFileExtension(this IFormFile file)
    {
        return Path.GetExtension(file.FileName);
    }
}
