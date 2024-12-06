using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InboxOutboxIndexesUpdate1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeSta~1",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastProcessingPi~",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "LastProcessingPingDate", "ForApplicationName", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_NextRetryProcess~",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "NextRetryProcessAfter", "ForApplicationName", "CreatedDate"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastProcessingPi~",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_NextRetryProcess~",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeSta~1",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "NextRetryProcessAfter", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastProcessingPingDate", "CreatedDate"]);
        }
    }
}
