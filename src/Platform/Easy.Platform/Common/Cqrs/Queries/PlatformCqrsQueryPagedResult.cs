using Easy.Platform.Common.Dtos;

namespace Easy.Platform.Common.Cqrs.Queries;

public abstract class PlatformCqrsQueryPagedResult<TItem> : IPlatformPagedResult<TItem>
{
    public PlatformCqrsQueryPagedResult() { }

    public PlatformCqrsQueryPagedResult(List<TItem> items, int totalCount, int? pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageSize = pageSize;
        if (pageSize != null) TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    public List<TItem> Items { get; set; }
    public long TotalCount { get; set; }
    public int? PageSize { get; set; }
    public int? TotalPages { get; }
}
