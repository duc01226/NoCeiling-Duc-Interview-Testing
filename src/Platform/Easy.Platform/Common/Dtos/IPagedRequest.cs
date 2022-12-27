namespace Easy.Platform.Common.Dtos;

public interface IPlatformPagedRequest : IPlatformDto
{
    int? SkipCount { get; set; }
    int? MaxResultCount { get; set; }

    public bool IsPagedRequestValid();

    public int? GetPageIndex()
    {
        if (SkipCount == null || MaxResultCount == null || MaxResultCount <= 0 || SkipCount < 0) return null;

        return SkipCount.Value / MaxResultCount.Value;
    }
}
