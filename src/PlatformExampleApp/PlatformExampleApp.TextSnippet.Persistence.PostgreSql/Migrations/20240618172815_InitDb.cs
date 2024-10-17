using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using PlatformExampleApp.TextSnippet.Domain.ValueObjects;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
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
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: true),
                    LastProcessingPingTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProcessError = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyUpdateToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationDataMigrationHistoryDbSet", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "PlatformInboxEventBusMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    JsonMessage = table.Column<string>(type: "text", nullable: true),
                    MessageTypeFullName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProduceFrom = table.Column<string>(type: "text", nullable: true),
                    RoutingKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ConsumerBy = table.Column<string>(type: "text", nullable: true),
                    ConsumeStatus = table.Column<string>(type: "text", nullable: false),
                    RetriedProcessCount = table.Column<int>(type: "integer", nullable: true),
                    ForApplicationName = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastConsumeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextRetryProcessAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastConsumeError = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyUpdateToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformInboxEventBusMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformOutboxEventBusMessage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    JsonMessage = table.Column<string>(type: "text", nullable: true),
                    MessageTypeFullName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RoutingKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SendStatus = table.Column<string>(type: "text", nullable: false),
                    RetriedProcessCount = table.Column<int>(type: "integer", nullable: true),
                    NextRetryProcessAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSendDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSendError = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyUpdateToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformOutboxEventBusMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TextSnippetEntity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SnippetText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FullText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    TimeOnly = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<ExampleAddressValueObject>(type: "jsonb", nullable: true),
                    AddressStrings = table.Column<List<string>>(type: "text[]", nullable: true),
                    Addresses = table.Column<List<ExampleAddressValueObject>>(type: "jsonb", nullable: true),
                    ConcurrencyUpdateToken = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "IX_PlatformInboxEventBusMessage_ForApplicationName_ConsumeStat~",
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
                name: "IX_PlatformOutboxEventBusMessage_SendStatus_LastSendDate_Creat~",
                table: "PlatformOutboxEventBusMessage",
                columns: ["SendStatus", "LastSendDate", "CreatedDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippet_FullText_FullTextSearch",
                table: "TextSnippetEntity",
                column: "FullText")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_TextSnippet_SnippetText_FullTextSearch",
                table: "TextSnippetEntity",
                column: "SnippetText")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", (string[])["gin_trgm_ops"])
                .Annotation("Npgsql:TsVectorConfig", "english");

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
