namespace Easy.Platform.Common.Dtos;

public interface IPlatformPagedResult<TItem> : IPlatformDto
{
    List<TItem> Items { get; set; }
    long TotalCount { get; set; }
    int? PageSize { get; set; }
}
