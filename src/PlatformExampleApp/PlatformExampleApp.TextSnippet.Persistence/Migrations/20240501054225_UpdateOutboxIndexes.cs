using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOutboxIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "PlatformOutboxEventBusMessage",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "PlatformInboxEventBusMessage",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastSendDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "LastConsumeDate", "CreatedDate"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_CreatedDate",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_CreatedDate",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "PlatformOutboxEventBusMessage",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "PlatformInboxEventBusMessage",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastSendDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "LastConsumeDate"]);
        }
    }
}
