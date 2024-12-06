using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InboxOutboxIndexesUpdate1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastProcessingPingDate_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_NextRetryProcessAfter_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastProcessingPingDate_ForApplicationName_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "LastProcessingPingDate", "ForApplicationName", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_NextRetryProcessAfter_ForApplicationName_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "NextRetryProcessAfter", "ForApplicationName", "CreatedDate"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastProcessingPingDate_ForApplicationName_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_NextRetryProcessAfter_ForApplicationName_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastProcessingPingDate_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastProcessingPingDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_NextRetryProcessAfter_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "NextRetryProcessAfter", "CreatedDate"]);
        }
    }
}
