using Easy.Platform.EfCore.EntityConfiguration;
using Easy.Platform.EfCore.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.EntityConfigurations;

internal sealed class TextSnippetEntityConfiguration : PlatformAuditedEntityConfiguration<TextSnippetEntity, Guid, Guid?>
{
    public override void Configure(EntityTypeBuilder<TextSnippetEntity> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.SnippetText)
            .HasMaxLength(TextSnippetEntity.SnippetTextMaxLength)
            .IsRequired();
        builder.Property(p => p.FullText)
            .HasMaxLength(TextSnippetEntity.FullTextMaxLength)
            .IsRequired();
        builder.OwnsOne(p => p.Address);
        builder.Property(p => p.Addresses).HasJsonConversion();
        builder.Property(p => p.TimeOnly)
            .HasConversion(
                v => v.ToString(),
                v => TimeOnly.Parse(v));
        builder.Property(p => p.AddressStrings).HasJsonConversion();

        // Do this to fix the warning
        // The entity type 'ExampleAddressValueObject' is an optional dependent using table sharing without any required non shared property that could be used to identify whether the entity exists.
        // If all nullable properties contain a null value in database then an object instance won't be created in the query. Add a required property to create instances with null values for other properties or mark the incoming navigation as required to always create an instance.
        //builder.Navigation(p => p.Address).IsRequired(); // Allow Address to be nullable

        builder.HasIndex(p => p.SnippetText).IsUnique();
    }
}
