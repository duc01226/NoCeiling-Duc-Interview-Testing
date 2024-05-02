using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
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
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "PlatformInboxEventBusMessage",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_Creat~",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastSendDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate_~",
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
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_Creat~",
                table: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_CreatedDate",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate_~",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "PlatformOutboxEventBusMessage",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "PlatformInboxEventBusMessage",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
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
