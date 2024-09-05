using Easy.Platform.Common.Dtos;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.Cqrs;

public interface IPlatformCqrsRequest : IPlatformDto<IPlatformCqrsRequest>, ICloneable
{
    public IPlatformCqrsRequestAuditInfo AuditInfo { get; set; }

    public TRequest SetAuditInfo<TRequest>(
        string auditTrackId,
        string auditRequestByUserId) where TRequest : class, IPlatformCqrsRequest;

    public TRequest SetAuditInfo<TRequest>(IPlatformCqrsRequestAuditInfo auditInfo) where TRequest : class, IPlatformCqrsRequest;

    /// <summary>
    /// Return a list of string which is used to build the cache key. Also could be used to filter, find the matched cache key
    /// by the array key parts to remove/update cache. The query object will be to json string. You could also convert it back when try to find cache key
    /// via request key parts to remove/update cache
    /// </summary>
    public static string[] BuildCacheRequestKeyParts<TRequest>(TRequest request, params string[] otherRequestKeyParts) where TRequest : class, IPlatformCqrsRequest
    {
        var requestJsonStr = request?.Clone().Cast<TRequest>().With(cqrsRequest => cqrsRequest.AuditInfo = null).ToJson();
        return new[] { requestJsonStr }.Concat(otherRequestKeyParts).ConcatSingle(typeof(TRequest).Name).ToArray();
    }
}

public class PlatformCqrsRequest : IPlatformCqrsRequest
{
    public virtual PlatformValidationResult<IPlatformCqrsRequest> Validate()
    {
        return PlatformValidationResult<IPlatformCqrsRequest>.Valid(value: this);
    }

    public IPlatformCqrsRequestAuditInfo AuditInfo { get; set; }

    public TRequest SetAuditInfo<TRequest>(
        string auditTrackId,
        string auditRequestByUserId) where TRequest : class, IPlatformCqrsRequest
    {
        AuditInfo = new PlatformCqrsRequestAuditInfo(auditTrackId, auditRequestByUserId);

        return this.As<TRequest>();
    }

    public TRequest SetAuditInfo<TRequest>(IPlatformCqrsRequestAuditInfo auditInfo) where TRequest : class, IPlatformCqrsRequest
    {
        AuditInfo = auditInfo;

        return this.As<TRequest>();
    }

    public object Clone()
    {
        return MemberwiseClone();
    }

    public virtual PlatformValidationResult<TRequest> Validate<TRequest>() where TRequest : IPlatformCqrsRequest
    {
        return PlatformValidationResult<IPlatformCqrsRequest>.Valid(value: this).Of<TRequest>();
    }
}

public interface IPlatformCqrsRequestAuditInfo
{
    public string AuditTrackId { get; }

    public DateTime AuditRequestDate { get; }

    public string AuditRequestByUserId { get; }
}

public class PlatformCqrsRequestAuditInfo : IPlatformCqrsRequestAuditInfo
{
    public PlatformCqrsRequestAuditInfo() { }

    public PlatformCqrsRequestAuditInfo(
        string auditTrackId,
        string auditRequestByUserId)
    {
        AuditTrackId = auditTrackId;
        AuditRequestDate = DateTime.UtcNow;
        AuditRequestByUserId = auditRequestByUserId;
    }

    public string AuditTrackId { get; } = Ulid.NewUlid().ToString();
    public DateTime AuditRequestDate { get; } = DateTime.UtcNow;
    public string AuditRequestByUserId { get; }
}
