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
        TotalPages = pageSize != null ? (int)Math.Ceiling((double)(totalCount / pageSize)) : null;
    }

    public PlatformCqrsQueryPagedResult(int totalCount, int? pageSize)
    {
        TotalCount = totalCount;
        PageSize = pageSize;
        TotalPages = pageSize != null ? (int)Math.Ceiling((double)(totalCount / pageSize)) : null;
    }

    public int? TotalPages { get; }

    public List<TItem> Items { get; set; }
    public long TotalCount { get; set; }
    public int? PageSize { get; set; }
}
