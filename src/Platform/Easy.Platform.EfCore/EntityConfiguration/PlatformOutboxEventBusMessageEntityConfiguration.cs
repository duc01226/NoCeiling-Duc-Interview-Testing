using Easy.Platform.Application.MessageBus.OutboxPattern;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Easy.Platform.EfCore.EntityConfiguration;

public sealed class PlatformOutboxEventBusMessageEntityConfiguration : PlatformEntityConfiguration<PlatformOutboxBusMessage, string>
{
    public const string PlatformOutboxBusMessageTableName = "PlatformOutboxEventBusMessage";

    public override void Configure(EntityTypeBuilder<PlatformOutboxBusMessage> builder)
    {
        base.Configure(builder);
        builder.ToTable(PlatformOutboxBusMessageTableName);
        builder.Property(p => p.Id).HasMaxLength(PlatformOutboxBusMessage.IdMaxLength);
        builder.Property(p => p.MessageTypeFullName)
            .HasMaxLength(PlatformOutboxBusMessage.MessageTypeFullNameMaxLength)
            .IsRequired();
        builder.Property(p => p.RoutingKey)
            .HasMaxLength(PlatformOutboxBusMessage.RoutingKeyMaxLength)
            .IsRequired();
        builder.Property(p => p.SendStatus)
            .HasConversion(new EnumToStringConverter<PlatformOutboxBusMessage.SendStatuses>());

        builder.HasIndex(p => p.RoutingKey);
        builder.HasIndex(
            p => new
            {
                p.SendStatus,
                p.LastSendDate
            });
        builder.HasIndex(
            p => new
            {
                p.SendStatus,
                p.NextRetryProcessAfter,
                p.LastSendDate
            });
        builder.HasIndex(p => new { p.LastSendDate, p.SendStatus });
        builder.HasIndex(p => p.NextRetryProcessAfter);
    }
}
