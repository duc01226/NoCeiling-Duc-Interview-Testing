using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InboxOutboxIndexesUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_CreatedDate_SendStatus",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_Creat~",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_CreatedDate_ConsumeStatus",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastProcessingPingDate",
                table: "PlatformOutboxEventBusMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastProcessingPingDate",
                table: "PlatformInboxEventBusMessage",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastProcessingPing~",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastProcessingPingDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_NextRetryProcessAf~",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "NextRetryProcessAfter", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeSta~1",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "NextRetryProcessAfter", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastProcessingPingDate", "CreatedDate"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastProcessingPing~",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_NextRetryProcessAf~",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeSta~1",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropColumn(
                name: "LastProcessingPingDate",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropColumn(
                name: "LastProcessingPingDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_CreatedDate_SendStatus",
                table: "PlatformOutboxEventBusMessage",
                columns: ["CreatedDate", "SendStatus"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_Creat~",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastSendDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_CreatedDate_ConsumeStatus",
                table: "PlatformInboxEventBusMessage",
                columns: ["CreatedDate", "ConsumeStatus"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastConsumeDate", "CreatedDate"]);
        }
    }
}
