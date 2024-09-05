using System.IO;
using Easy.Platform.Common.Extensions;
using Microsoft.AspNetCore.Http;

namespace Easy.Platform.Infrastructures.FileStorage;

public interface IPlatformFile
{
    public string GetFileName();
    public Stream OpenReadStream();
    public Task<byte[]> GetFileBinaries();
}

public class PlatformHttpFormFile : IPlatformFile
{
    public IFormFile FormFile { get; set; }

    public string GetFileName()
    {
        return FormFile.FileName;
    }

    public Stream OpenReadStream()
    {
        return FormFile.OpenReadStream().With(s => s.Position = 0);
    }

    public Task<byte[]> GetFileBinaries()
    {
        return FormFile.GetFileBinaries();
    }

    public static PlatformHttpFormFile Create(IFormFile formFile)
    {
        return new PlatformHttpFormFile
        {
            FormFile = formFile
        };
    }
}

public class PlatformStreamFile : IPlatformFile
{
    public Stream Stream { get; set; }
    public string FileName { get; set; }

    public string GetFileName()
    {
        return FileName;
    }

    public Stream OpenReadStream()
    {
        return Stream.With(s => s.Position = 0);
    }

    public Task<byte[]> GetFileBinaries()
    {
        return Stream.GetBinaries();
    }

    public static PlatformStreamFile Create(Stream stream, string fileName)
    {
        return new PlatformStreamFile
        {
            Stream = stream,
            FileName = fileName
        };
    }
}
