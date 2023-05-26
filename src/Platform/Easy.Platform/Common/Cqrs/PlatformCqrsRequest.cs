using Easy.Platform.Common.Dtos;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.Cqrs;

public interface IPlatformCqrsRequest : IPlatformDto<IPlatformCqrsRequest>
{
    public IPlatformCqrsRequestAuditInfo AuditInfo { get; }

    public TRequest SetAuditInfo<TRequest>(
        Guid auditTrackId,
        string auditRequestByUserId) where TRequest : class, IPlatformCqrsRequest;

    public TRequest SetAuditInfo<TRequest>(IPlatformCqrsRequestAuditInfo auditInfo) where TRequest : class, IPlatformCqrsRequest;
}

public class PlatformCqrsRequest : IPlatformCqrsRequest
{
    public virtual PlatformValidationResult<IPlatformCqrsRequest> Validate()
    {
        return PlatformValidationResult<IPlatformCqrsRequest>.Valid(value: this);
    }

    public IPlatformCqrsRequestAuditInfo AuditInfo { get; private set; } = new PlatformCqrsRequestAuditInfo();

    public TRequest SetAuditInfo<TRequest>(
        Guid auditTrackId,
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

    public virtual PlatformValidationResult<TRequest> Validate<TRequest>() where TRequest : IPlatformCqrsRequest
    {
        return PlatformValidationResult<IPlatformCqrsRequest>.Valid(value: this).Of<TRequest>();
    }
}

public interface IPlatformCqrsRequestAuditInfo
{
    public Guid AuditTrackId { get; }

    public DateTime AuditRequestDate { get; }

    public string AuditRequestByUserId { get; }
}

public sealed class PlatformCqrsRequestAuditInfo : IPlatformCqrsRequestAuditInfo
{
    public PlatformCqrsRequestAuditInfo() { }

    public PlatformCqrsRequestAuditInfo(
        Guid auditTrackId,
        string auditRequestByUserId)
    {
        AuditTrackId = auditTrackId;
        AuditRequestDate = DateTime.UtcNow;
        AuditRequestByUserId = auditRequestByUserId;
    }

    public Guid AuditTrackId { get; } = Guid.NewGuid();
    public DateTime AuditRequestDate { get; } = DateTime.UtcNow;
    public string AuditRequestByUserId { get; }
}
