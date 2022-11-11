using Easy.Platform.Common.Dtos;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Common.Cqrs;

public interface IPlatformCqrsRequest : IPlatformDto<IPlatformCqrsRequest>
{
    public Guid? AuditTrackId { get; }

    public DateTime? AuditRequestDate { get; }

    public string AuditRequestByUserId { get; }

    public IPlatformCqrsRequest PopulateAuditInfo(
        Guid? auditTrackId,
        DateTime? auditRequestDate,
        string auditRequestByUserId);
}

public class PlatformCqrsRequest : IPlatformCqrsRequest
{
    public Guid? AuditTrackId { get; private set; }

    public DateTime? AuditRequestDate { get; private set; }

    public string AuditRequestByUserId { get; private set; }

    public IPlatformCqrsRequest PopulateAuditInfo(
        Guid? auditTrackId,
        DateTime? auditRequestDate,
        string auditRequestByUserId)
    {
        AuditTrackId = auditTrackId;
        AuditRequestDate = auditRequestDate;
        AuditRequestByUserId = auditRequestByUserId;

        return this;
    }

    public virtual PlatformValidationResult<IPlatformCqrsRequest> Validate()
    {
        return PlatformValidationResult<IPlatformCqrsRequest>.Valid(value: this);
    }
}
