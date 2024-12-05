using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
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
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_CreatedDate",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_CreatedDate_ConsumeStatus",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastProcessingPingDate",
                table: "PlatformOutboxEventBusMessage",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastProcessingPingDate",
                table: "PlatformInboxEventBusMessage",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyUpdateToken",
                table: "ApplicationDataMigrationHistoryDbSet",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastProcessingPingDate_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastProcessingPingDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_NextRetryProcessAfter_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "NextRetryProcessAfter", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastProcessingPingDate_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastProcessingPingDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_NextRetryProcessAfter_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "NextRetryProcessAfter", "CreatedDate"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastProcessingPingDate_CreatedDate",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_NextRetryProcessAfter_CreatedDate",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastProcessingPingDate_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_NextRetryProcessAfter_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropColumn(
                name: "LastProcessingPingDate",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropColumn(
                name: "LastProcessingPingDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.AlterColumn<string>(
                name: "ConcurrencyUpdateToken",
                table: "ApplicationDataMigrationHistoryDbSet",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_CreatedDate_SendStatus",
                table: "PlatformOutboxEventBusMessage",
                columns: ["CreatedDate", "SendStatus"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastSendDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_CreatedDate_ConsumeStatus",
                table: "PlatformInboxEventBusMessage",
                columns: ["CreatedDate", "ConsumeStatus"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastConsumeDate", "CreatedDate"]);
        }
    }
}
