using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationDataMigrationHistoryDbSet",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDataMigrationHistoryDbSet", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "PlatformInboxEventBusMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JsonMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessageTypeFullName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProduceFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoutingKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConsumerBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsumeStatus = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RetriedProcessCount = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastConsumeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextRetryProcessAfter = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastConsumeError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyUpdateToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformInboxEventBusMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformOutboxEventBusMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JsonMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessageTypeFullName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RoutingKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SendStatus = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RetriedProcessCount = table.Column<int>(type: "int", nullable: true),
                    NextRetryProcessAfter = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSendDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSendError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyUpdateToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformOutboxEventBusMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TextSnippetEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnippetText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AddressNumber = table.Column<string>(name: "Address_Number", type: "nvarchar(max)", nullable: true),
                    AddressStreet = table.Column<string>(name: "Address_Street", type: "nvarchar(max)", nullable: true),
                    AddressStrings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Addresses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyUpdateToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastUpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextSnippetEntity", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate",
                table: "PlatformInboxEventBusMessage",
                columns: new[] { "ConsumeStatus", "LastConsumeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_NextRetryProcessAfter_LastConsumeDate",
                table: "PlatformInboxEventBusMessage",
                columns: new[] { "ConsumeStatus", "NextRetryProcessAfter", "LastConsumeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_LastConsumeDate_ConsumeStatus",
                table: "PlatformInboxEventBusMessage",
                columns: new[] { "LastConsumeDate", "ConsumeStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_RoutingKey",
                table: "PlatformInboxEventBusMessage",
                column: "RoutingKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_LastSendDate_SendStatus",
                table: "PlatformOutboxEventBusMessage",
                columns: new[] { "LastSendDate", "SendStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_NextRetryProcessAfter",
                table: "PlatformOutboxEventBusMessage",
                column: "NextRetryProcessAfter");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_RoutingKey",
                table: "PlatformOutboxEventBusMessage",
                column: "RoutingKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate",
                table: "PlatformOutboxEventBusMessage",
                columns: new[] { "SendStatus", "LastSendDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_NextRetryProcessAfter_LastSendDate",
                table: "PlatformOutboxEventBusMessage",
                columns: new[] { "SendStatus", "NextRetryProcessAfter", "LastSendDate" });

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
