using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformExampleApp.TextSnippet.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class TestUsingTimeOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "TimeOnly",
                table: "TextSnippetEntity",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.CreateIndex(
                name: "IX_Discussion_Detail_FTS",
                table: "DiscussionPoint",
                column: "Detail")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" })
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_EmployeeEmail_FTS",
                table: "Employee",
                column: "EmployeeEmail")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" })
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_Employee_FullName_FTS",
                table: "Employee",
                column: "FullName")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" })
                .Annotation("Npgsql:TsVectorConfig", "english");


            migrationBuilder.CreateIndex(
                name: "IX_Employee_UserEmail_FTS",
                table: "Employee",
                column: "UserEmail")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" })
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnit_Name",
                table: "OrganizationalUnit",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email_FTS",
                table: "User",
                column: "Email")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" })
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.CreateIndex(
                name: "IX_User_FullName_FTS",
                table: "User",
                column: "FullName")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" })
                .Annotation("Npgsql:TsVectorConfig", "english");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeOnly",
                table: "TextSnippetEntity");

            migrationBuilder.DropIndex(
                name: "IX_Discussion_Detail_FTS",
                table: "DiscussionPoint");

            migrationBuilder.DropIndex(
                name: "IX_Employee_EmployeeEmail_FTS",
                table: "Employee");

            migrationBuilder.DropIndex(
                name: "IX_Employee_FullName_FTS",
                table: "Employee");

            migrationBuilder.DropIndex(
                name: "IX_Employee_UserEmail_FTS",
                table: "Employee");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationalUnit_Name",
                table: "OrganizationalUnit");

            migrationBuilder.DropIndex(
                name: "IX_User_Email_FTS",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_User_FullName_FTS",
                table: "User");
        }
    }
}
