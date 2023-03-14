using System.IO;
using System.Reflection;

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

        public static Stream ReadAsStreamFromBase64(string base64)
        {
            return new MemoryStream(Convert.FromBase64String(base64));
        }

        public static string ReadCurrentDirectoryFileAsString(string filePathFromCurrentExecutingDirectory)
        {
            return ReadFileAsString(
                Path.ConcatRelativePath(
                    System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    filePathFromCurrentExecutingDirectory));
        }
    }
}
