namespace Easy.Platform.Domain.Entities;

public interface IAuditedDateEntity
{
    public DateTime? CreatedDate { get; set; }

    public DateTime? LastUpdatedDate { get; set; }
}

public interface IAuditedEntity<TUserId> : IAuditedDateEntity
{
    public TUserId CreatedBy { get; set; }

    public TUserId LastUpdatedBy { get; set; }
}

public abstract class RootAuditedEntity<TEntity, TPrimaryKey, TUserId> : RootEntity<TEntity, TPrimaryKey>, IAuditedEntity<TUserId>
    where TEntity : Entity<TEntity, TPrimaryKey>, new()
{
    private TUserId lastUpdatedBy;
    private DateTime? lastUpdatedDate;

    public RootAuditedEntity()
    {
        CreatedDate ??= DateTime.UtcNow;
        LastUpdatedDate ??= CreatedDate;
    }

    public RootAuditedEntity(TUserId createdBy) : this()
    {
        CreatedBy = createdBy;
        LastUpdatedBy ??= CreatedBy;
    }

    public TUserId CreatedBy { get; set; }

    public TUserId LastUpdatedBy
    {
        get => lastUpdatedBy ?? CreatedBy;
        set => lastUpdatedBy = value;
    }

    public DateTime? CreatedDate { get; set; }

    public DateTime? LastUpdatedDate
    {
        get => lastUpdatedDate ?? CreatedDate;
        set => lastUpdatedDate = value;
    }
}
