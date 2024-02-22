using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFtsFixForPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TextSnippetEntity_SnippetText_FullText",
                table: "TextSnippetEntity");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippet_FullText_FullTextSearch",
                table: "TextSnippetEntity",
                column: "FullText")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TextSnippet_FullText_FullTextSearch",
                table: "TextSnippetEntity");

            migrationBuilder.DropIndex(
                name: "IX_TextSnippet_SnippetText_FullTextSearch",
                table: "TextSnippetEntity");

            migrationBuilder.DropIndex(
                name: "IX_TextSnippetEntity_SnippetText",
                table: "TextSnippetEntity");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_SnippetText_FullText",
                table: "TextSnippetEntity",
                columns: ["SnippetText", "FullText"])
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");
        }
    }
}
