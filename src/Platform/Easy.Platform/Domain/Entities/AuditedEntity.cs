using Easy.Platform.Common.Timing;

namespace Easy.Platform.Domain.Entities;

public interface IAuditedEntity<TUserId>
{
    public TUserId CreatedBy { get; set; }

    public TUserId LastUpdatedBy { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastUpdatedDate { get; set; }
}

public abstract class RootAuditedEntity<TEntity, TPrimaryKey, TUserId> : RootEntity<TEntity, TPrimaryKey>, IAuditedEntity<TUserId>
    where TEntity : Entity<TEntity, TPrimaryKey>, new()
{
    public RootAuditedEntity() { }

    public RootAuditedEntity(TUserId createdBy = default)
    {
        CreatedDate ??= Clock.Now;
        LastUpdatedDate ??= CreatedDate;
        CreatedBy = createdBy;
        LastUpdatedBy ??= CreatedBy;
    }

    public TUserId CreatedBy { get; set; }
    public TUserId LastUpdatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
}
