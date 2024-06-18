using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationDataMigrationHistoryDbSet",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LastProcessingPingTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastProcessError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConcurrencyUpdateToken = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDataMigrationHistoryDbSet", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "PlatformInboxEventBusMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    JsonMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessageTypeFullName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProduceFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoutingKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConsumerBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsumeStatus = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RetriedProcessCount = table.Column<int>(type: "int", nullable: true),
                    ForApplicationName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastConsumeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextRetryProcessAfter = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastConsumeError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyUpdateToken = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformInboxEventBusMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformOutboxEventBusMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    JsonMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessageTypeFullName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RoutingKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SendStatus = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RetriedProcessCount = table.Column<int>(type: "int", nullable: true),
                    NextRetryProcessAfter = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSendDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSendError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyUpdateToken = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformOutboxEventBusMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TextSnippetEntity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SnippetText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TimeOnly = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address_Number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address_Street = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AddressStrings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyUpdateToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextSnippetEntity", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationDataMigrationHistoryDbSet_Status",
                table: "ApplicationDataMigrationHistoryDbSet",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ConsumeStatus", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_CreatedDate_ConsumeStatus",
                table: "PlatformInboxEventBusMessage",
                columns: ["CreatedDate", "ConsumeStatus"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStatus_LastConsumeDate_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: ["ForApplicationName", "ConsumeStatus", "LastConsumeDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_CreatedDate_SendStatus",
                table: "PlatformOutboxEventBusMessage",
                columns: ["CreatedDate", "SendStatus"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastSendDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_CreatedBy",
                table: "TextSnippetEntity",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_CreatedDate",
                table: "TextSnippetEntity",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_LastUpdatedBy",
                table: "TextSnippetEntity",
                column: "LastUpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_LastUpdatedDate",
                table: "TextSnippetEntity",
                column: "LastUpdatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_SnippetText",
                table: "TextSnippetEntity",
                column: "SnippetText",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationDataMigrationHistoryDbSet");

            migrationBuilder.DropTable(
                name: "PlatformInboxEventBusMessage");

            migrationBuilder.DropTable(
                name: "PlatformOutboxEventBusMessage");

            migrationBuilder.DropTable(
                name: "TextSnippetEntity");
        }
    }
}
