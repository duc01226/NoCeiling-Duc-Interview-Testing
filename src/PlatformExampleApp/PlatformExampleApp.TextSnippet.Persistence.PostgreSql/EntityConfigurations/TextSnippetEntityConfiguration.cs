using Easy.Platform.EfCore.EntityConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformExampleApp.TextSnippet.Domain.Entities;

namespace PlatformExampleApp.TextSnippet.Persistence.EntityConfigurations;

internal class TextSnippetEntityConfiguration : PlatformAuditedEntityConfiguration<TextSnippetEntity, Guid, Guid?>
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

        builder.Property(p => p.Addresses).HasColumnType("jsonb");
        builder.Property(p => p.Address).HasColumnType("jsonb");
        builder.Property(p => p.AddressStrings);
        //builder.OwnsOne(
        //    p => p.Address,
        //    builder =>
        //    {
        //        // Support fulltext search index. References: https://www.npgsql.org/efcore/mapping/full-text-search.html#method-2-expression-index
        //        builder
        //            .HasIndex(p => new { p.Street, p.Number })
        //            .HasMethod("GIN")
        //            .IsTsVectorExpressionIndex("english");
        //    });

        // Do this to fix the warning
        // The entity type 'ExampleAddressValueObject' is an optional dependent using table sharing without any required non shared property that could be used to identify whether the entity exists.
        // If all nullable properties contain a null value in database then an object instance won't be created in the query. Add a required property to create instances with null values for other properties or mark the incoming navigation as required to always create an instance.
        //builder.Navigation(p => p.Address).IsRequired(); // Allow Address to be nullable

        // Support fulltext search index. References: https://www.npgsql.org/efcore/mapping/full-text-search.html#method-2-expression-index
        builder
            .HasIndex(p => p.SnippetText)
            .HasMethod("GIN")
            .IsTsVectorExpressionIndex("english");

        builder
            .HasIndex(p => p.Addresses)
            .HasMethod("GIN");
        builder
            .HasIndex(p => p.AddressStrings)
            .HasMethod("GIN");
        builder
            .HasIndex(p => p.Address)
            .HasMethod("GIN");
    }
}
