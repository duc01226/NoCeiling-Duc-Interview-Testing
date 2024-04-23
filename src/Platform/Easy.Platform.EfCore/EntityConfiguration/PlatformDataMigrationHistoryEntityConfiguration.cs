using Easy.Platform.Persistence.DataMigration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Easy.Platform.EfCore.EntityConfiguration;

public class PlatformDataMigrationHistoryEntityConfiguration : IEntityTypeConfiguration<PlatformDataMigrationHistory>
{
    public void Configure(EntityTypeBuilder<PlatformDataMigrationHistory> builder)
    {
        builder.HasKey(p => p.Name);
        builder.Property(p => p.ConcurrencyUpdateToken).IsConcurrencyToken();
        builder.Property(p => p.Status).HasConversion(new EnumToStringConverter<PlatformDataMigrationHistory.Statuses>());

        builder.HasIndex(p => p.ConcurrencyUpdateToken);
        builder.HasIndex(p => p.Status);
    }
}
