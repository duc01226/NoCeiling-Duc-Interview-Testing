using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInboxOutboxIndexes1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_CreatedDate_SendStatus",
                table: "PlatformOutboxEventBusMessage",
                columns: ["CreatedDate", "SendStatus"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_CreatedDate_SendStatus",
                table: "PlatformOutboxEventBusMessage");
        }
    }
}
