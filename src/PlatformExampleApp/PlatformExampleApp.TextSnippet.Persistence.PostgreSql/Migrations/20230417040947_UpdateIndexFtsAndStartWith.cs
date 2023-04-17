using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIndexFtsAndStartWith : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm");

            migrationBuilder.DropIndex(
                name: "IX_TextSnippet_SnippetText_FullTextSearch",
                table: "TextSnippetEntity");

            migrationBuilder.DropIndex(
                name: "IX_TextSnippetEntity_SnippetText",
                table: "TextSnippetEntity");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippet_SnippetText_FullTextSearch",
                table: "TextSnippetEntity",
                column: "SnippetText")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" })
                .Annotation("Npgsql:TsVectorConfig", "english");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TextSnippet_SnippetText_FullTextSearch",
                table: "TextSnippetEntity");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippet_SnippetText_FullTextSearch",
                table: "TextSnippetEntity",
                column: "SnippetText")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_SnippetText",
                table: "TextSnippetEntity",
                column: "SnippetText");
        }
    }
}
