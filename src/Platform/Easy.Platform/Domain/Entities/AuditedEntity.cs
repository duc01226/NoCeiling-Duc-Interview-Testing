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
    public RootAuditedEntity()
    {
        CreatedDate ??= DateTime.UtcNow;
        LastUpdatedDate ??= CreatedDate;
    }

    public RootAuditedEntity(TUserId createdBy = default) : this()
    {
        CreatedBy = createdBy;
        LastUpdatedBy ??= CreatedBy;
    }

    public TUserId CreatedBy { get; set; }
    public TUserId LastUpdatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
}
