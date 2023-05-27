using Easy.Platform.Persistence.DataMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Easy.Platform.EfCore.EntityConfiguration;

public class PlatformDataMigrationHistoryEntityConfiguration : IEntityTypeConfiguration<PlatformDataMigrationHistory>
{
    public void Configure(EntityTypeBuilder<PlatformDataMigrationHistory> builder)
    {
        builder.HasKey(p => p.Name);
    }
}
