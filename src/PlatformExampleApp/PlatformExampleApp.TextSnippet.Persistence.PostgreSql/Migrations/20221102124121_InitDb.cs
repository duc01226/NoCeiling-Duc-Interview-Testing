using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformExampleApp.TextSnippet.Domain.ValueObjects;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
{
    public partial class InitDb : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationDataMigrationHistoryDbSet",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDataMigrationHistoryDbSet", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "PlatformInboxEventBusMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JsonMessage = table.Column<string>(type: "text", nullable: true),
                    MessageTypeFullName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProduceFrom = table.Column<string>(type: "text", nullable: true),
                    RoutingKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ConsumerBy = table.Column<string>(type: "text", nullable: true),
                    ConsumeStatus = table.Column<string>(type: "text", nullable: false),
                    RetriedProcessCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastConsumeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextRetryProcessAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastConsumeError = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyUpdateToken = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformInboxEventBusMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformOutboxEventBusMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JsonMessage = table.Column<string>(type: "text", nullable: true),
                    MessageTypeFullName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RoutingKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SendStatus = table.Column<string>(type: "text", nullable: false),
                    RetriedProcessCount = table.Column<int>(type: "integer", nullable: true),
                    NextRetryProcessAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSendDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSendError = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyUpdateToken = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformOutboxEventBusMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TextSnippetEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnippetText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FullText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Address = table.Column<ExampleAddressValueObject>(type: "jsonb", nullable: true),
                    AddressStrings = table.Column<List<string>>(type: "text[]", nullable: true),
                    Addresses = table.Column<List<ExampleAddressValueObject>>(type: "jsonb", nullable: true),
                    ConcurrencyUpdateToken = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextSnippetEntity", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                columns: new[] { "ConsumeStatus", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_LastConsumeDate",
                table: "PlatformInboxEventBusMessage",
                columns: new[] { "ConsumeStatus", "LastConsumeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_ConsumeStatus_NextRetryProcess~",
                table: "PlatformInboxEventBusMessage",
                columns: new[] { "ConsumeStatus", "NextRetryProcessAfter", "LastConsumeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_CreatedDate",
                table: "PlatformInboxEventBusMessage",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_LastConsumeDate",
                table: "PlatformInboxEventBusMessage",
                column: "LastConsumeDate");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_NextRetryProcessAfter",
                table: "PlatformInboxEventBusMessage",
                column: "NextRetryProcessAfter");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInboxEventBusMessage_RoutingKey",
                table: "PlatformInboxEventBusMessage",
                column: "RoutingKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_LastSendDate",
                table: "PlatformOutboxEventBusMessage",
                column: "LastSendDate");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_NextRetryProcessAfter",
                table: "PlatformOutboxEventBusMessage",
                column: "NextRetryProcessAfter");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_RoutingKey",
                table: "PlatformOutboxEventBusMessage",
                column: "RoutingKey");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_CreatedDate",
                table: "PlatformOutboxEventBusMessage",
                columns: new[] { "SendStatus", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate",
                table: "PlatformOutboxEventBusMessage",
                columns: new[] { "SendStatus", "LastSendDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_NextRetryProcessAf~",
                table: "PlatformOutboxEventBusMessage",
                columns: new[] { "SendStatus", "NextRetryProcessAfter", "LastSendDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_Address",
                table: "TextSnippetEntity",
                column: "Address")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_Addresses",
                table: "TextSnippetEntity",
                column: "Addresses")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippetEntity_AddressStrings",
                table: "TextSnippetEntity",
                column: "AddressStrings")
                .Annotation("Npgsql:IndexMethod", "GIN");

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
                column: "SnippetText")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");
        }

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
