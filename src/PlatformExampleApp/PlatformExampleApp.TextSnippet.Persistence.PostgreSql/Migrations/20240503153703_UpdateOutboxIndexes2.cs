using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOutboxIndexes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate_~",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.AddColumn<string>(
                name: "ForApplicationName",
                table: "PlatformInboxEventBusMessage",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastConsumeDate", "CreatedDate"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.DropColumn(
                name: "ForApplicationName",
                table: "PlatformInboxEventBusMessage");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate_~",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "LastConsumeDate", "CreatedDate"]);
        }
    }
}
