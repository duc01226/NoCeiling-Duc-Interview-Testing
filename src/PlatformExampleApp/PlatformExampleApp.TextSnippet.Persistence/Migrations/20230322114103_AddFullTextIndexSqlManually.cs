using Easy.Platform.EfCore.Utils;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformExampleApp.TextSnippet.Domain.Entities;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextIndexSqlManually : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SqlServerMigrationUtil.CreateFullTextCatalogIfNotExists(migrationBuilder, $"FTS_{nameof(TextSnippetEntity)}");
            SqlServerMigrationUtil.CreateFullTextIndexIfNotExists(
                migrationBuilder,
                tableName: "TextSnippetEntity",
                columnNames: new List<string>
                    { nameof(TextSnippetEntity.SnippetText), nameof(TextSnippetEntity.FullText) },
                keyIndex: "PK_TextSnippetEntity",
                fullTextCatalog: $"FTS_{nameof(TextSnippetEntity)}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            SqlServerMigrationUtil.DropFullTextIndexIfExists(migrationBuilder, "TextSnippetEntity");
            SqlServerMigrationUtil.DropFullTextCatalogIfExists(migrationBuilder, $"FTS_{nameof(TextSnippetEntity)}");
        }
    }
}
