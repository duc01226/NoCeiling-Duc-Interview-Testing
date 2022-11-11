namespace Easy.Platform.Common.Dtos;

public interface IPlatformPagedRequest<TRequest> : IPlatformDto<TRequest>
    where TRequest : IPlatformDto<TRequest>
{
    int? SkipCount { get; set; }
    int? MaxResultCount { get; set; }

    public bool IsPagedRequestValid();
}
