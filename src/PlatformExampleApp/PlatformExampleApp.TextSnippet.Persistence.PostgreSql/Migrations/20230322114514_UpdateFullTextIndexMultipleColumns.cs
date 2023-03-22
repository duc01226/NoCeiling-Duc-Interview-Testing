using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFullTextIndexMultipleColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TextSnippetEntity_SnippetText",
                table: "TextSnippetEntity");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_SnippetText_FullText",
                table: "TextSnippetEntity",
                columns: new[] { "SnippetText", "FullText" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TextSnippetEntity_SnippetText_FullText",
                table: "TextSnippetEntity");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_SnippetText",
                table: "TextSnippetEntity",
                column: "SnippetText")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");
        }
    }
}
