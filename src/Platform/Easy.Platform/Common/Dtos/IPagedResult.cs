namespace Easy.Platform.Common.Dtos;

public interface IPlatformPagedResult<TItem> : IPlatformDto
{
    List<TItem> Items { get; set; }
    long TotalCount { get; set; }
    int? PageSize { get; set; }
    int? SkipCount { get; set; }
    int? TotalPages { get; }
    int? PageIndex { get; }

    public int? GetPageIndex()
    {
        if (SkipCount == null || PageSize == null || PageSize <= 0 || SkipCount < 0) return null;

        return (SkipCount.Value / PageSize.Value) + 1;
    }
}
