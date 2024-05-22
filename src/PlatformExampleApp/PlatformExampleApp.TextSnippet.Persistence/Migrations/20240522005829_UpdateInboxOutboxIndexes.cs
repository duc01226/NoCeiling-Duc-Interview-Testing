using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInboxOutboxIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_CreatedDate_ConsumeStatus",
                table: "PlatformInboxEventBusMessage",
                columns: ["CreatedDate", "ConsumeStatus"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_CreatedDate_ConsumeStatus",
                table: "PlatformInboxEventBusMessage");
        }
    }
}
