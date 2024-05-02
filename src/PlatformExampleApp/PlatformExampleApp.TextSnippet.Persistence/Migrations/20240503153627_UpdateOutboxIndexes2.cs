using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOutboxIndexes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.AddColumn<string>(
                name: "ForApplicationName",
                table: "PlatformInboxEventBusMessage",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastConsumeDate", "CreatedDate"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropColumn(
                name: "ForApplicationName",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "LastConsumeDate", "CreatedDate"]);
        }
    }
}
