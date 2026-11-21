using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCenterDeletionDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "due_at",
                schema: "app",
                table: "center_deletion_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "extended_at",
                schema: "app",
                table: "center_deletion_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extension_note",
                schema: "app",
                table: "center_deletion_requests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "due_at",
                schema: "app",
                table: "center_deletion_requests");

            migrationBuilder.DropColumn(
                name: "extended_at",
                schema: "app",
                table: "center_deletion_requests");

            migrationBuilder.DropColumn(
                name: "extension_note",
                schema: "app",
                table: "center_deletion_requests");
        }
    }
}
