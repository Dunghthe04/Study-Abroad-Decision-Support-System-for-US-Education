using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationToProfileActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duration_months",
                schema: "app",
                table: "profile_activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_ongoing",
                schema: "app",
                table: "profile_activities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_months",
                schema: "app",
                table: "profile_activities");

            migrationBuilder.DropColumn(
                name: "is_ongoing",
                schema: "app",
                table: "profile_activities");
        }
    }
}
