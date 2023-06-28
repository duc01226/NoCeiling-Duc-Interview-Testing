using Easy.Platform.Common.Dtos;
using Easy.Platform.Common.Extensions;

namespace Easy.Platform.Common.Cqrs.Queries;

public abstract class PlatformCqrsQueryPagedResult<TItem> : IPlatformPagedResult<TItem>
{
    protected PlatformCqrsQueryPagedResult() { }

    public PlatformCqrsQueryPagedResult(List<TItem> items, long totalCount, IPlatformPagedRequest pagedRequest)
    {
        Items = items;
        TotalCount = totalCount;
        PageSize = pagedRequest.MaxResultCount;
        SkipCount = pagedRequest.SkipCount;
    }

    public List<TItem> Items { get; set; }
    public long TotalCount { get; set; }
    public int? PageSize { get; set; }
    public int? SkipCount { get; set; }
    public int? TotalPages => PageSize != null ? (int)Math.Ceiling(TotalCount / (double)PageSize) : null;
    public int? PageIndex => this.As<IPlatformPagedResult<TItem>>().GetPageIndex();
}
