using System.IO;
using System.Reflection;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Utils;

public static partial class Util
{
    public static class FileReader
    {
        /// <summary>
        /// Reads all characters from the current position to the end of the stream.
        /// </summary>
        /// <param name="filePath">filePath</param>
        /// <returns>All file content as string</returns>
        public static async Task<string> ReadFileAsStringAsync(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            {
                var result = reader.ReadToEndAsync().GetResult();

                return result;
            }
        }

        public static async Task<byte[]> ReadStreamAsBytesAsync(Func<Stream> openReadStream)
        {
            using (var reader = new StreamReader(openReadStream()))
            {
                using (var ms = new MemoryStream())
                {
                    await reader.BaseStream.CopyToAsync(ms);

                    return ms.ToArray();
                }
            }
        }

        public static Stream ReadBase64AsStream(string base64)
        {
            return new MemoryStream(Convert.FromBase64String(base64));
        }

        public static async Task<string> ReadApplicationFileAsStringAsync(string applicationRelativeFilePath)
        {
            return await ReadFileAsStringAsync(
                PathBuilder.ConcatRelativePath(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    applicationRelativeFilePath));
        }

        public static bool CheckFileExistsByRelativeToEntryExecutionPath(string relativeToEntryExecutionFilePath)
        {
            var fullPath = PathBuilder.GetFullPathByRelativeToEntryExecutionPath(relativeToEntryExecutionFilePath);

            return File.Exists(fullPath);
        }

        /// <summary>
        /// Reads all characters from the current position to the end of the stream.
        /// </summary>
        /// <param name="filePath">filePath</param>
        /// <returns>All file content as string</returns>
        public static string ReadFileAsString(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            {
                return reader.ReadToEnd();
            }
        }

        public static byte[] ReadFileAsBytes(Func<Stream> openReadStream)
        {
            using (var reader = new StreamReader(openReadStream()))
            {
                using (var ms = new MemoryStream())
                {
                    reader.BaseStream.CopyTo(ms);

                    return ms.ToArray();
                }
            }
        }

        public static string ReadApplicationFileAsString(string applicationRelativeFilePath)
        {
            return ReadFileAsString(
                PathBuilder.ConcatRelativePath(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    applicationRelativeFilePath));
        }
    }
}
