using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParentProfileTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "profile_transfer_at",
                schema: "app",
                table: "parent_student_links",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profile_transfer_status",
                schema: "app",
                table: "parent_student_links",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "profile_transfer_at",
                schema: "app",
                table: "parent_student_links");

            migrationBuilder.DropColumn(
                name: "profile_transfer_status",
                schema: "app",
                table: "parent_student_links");
        }
    }
}
