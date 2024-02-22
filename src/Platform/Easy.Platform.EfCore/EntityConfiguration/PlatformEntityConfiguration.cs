using Easy.Platform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Easy.Platform.EfCore.EntityConfiguration;

public abstract class PlatformEntityConfiguration
{
    public static void ConfigureRowVersionEntity<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IEntity
    {
        if (typeof(TEntity).IsAssignableTo(typeof(IRowVersionEntity)))
            builder.Property(p => ((IRowVersionEntity)p).ConcurrencyUpdateToken).IsConcurrencyToken();
    }
}

public abstract class PlatformEntityConfiguration<TEntity>
    : PlatformEntityConfiguration, IEntityTypeConfiguration<TEntity>
    where TEntity : class, IEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ConfigureRowVersionEntity(builder);
    }
}

public abstract class PlatformEntityConfiguration<TEntity, TPrimaryKey>
    : PlatformEntityConfiguration, IEntityTypeConfiguration<TEntity>
    where TEntity : class, IEntity<TPrimaryKey>
{
    public virtual bool AutoIndexUserAuditInfo => true;

    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(p => p.Id);
        ConfigureRowVersionEntity(builder);
        if (AutoIndexUserAuditInfo) IndexUserAuditInfo(builder);
    }

    private void IndexUserAuditInfo(EntityTypeBuilder<TEntity> builder)
    {
        if (typeof(TEntity).IsAssignableTo(typeof(IUserAuditedEntity<TPrimaryKey>)))
        {
            builder.HasIndex(nameof(IUserAuditedEntity<TPrimaryKey>.CreatedBy));
            builder.HasIndex(nameof(IUserAuditedEntity<TPrimaryKey>.LastUpdatedBy));
        }
    }
}

public abstract class PlatformAuditedEntityConfiguration<TEntity, TPrimaryKey, TUserId>
    : PlatformEntityConfiguration, IEntityTypeConfiguration<TEntity>
    where TEntity : class, IEntity<TPrimaryKey>, IFullAuditedEntity<TUserId>
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(p => p.Id);

        ConfigureFullAuditInfoEntity(builder);
        ConfigureRowVersionEntity(builder);
    }

    private static void ConfigureFullAuditInfoEntity(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasIndex(p => p.CreatedBy);
        builder.HasIndex(p => p.CreatedDate);
        builder.HasIndex(p => p.LastUpdatedBy);
        builder.HasIndex(p => p.LastUpdatedDate);
    }
}
