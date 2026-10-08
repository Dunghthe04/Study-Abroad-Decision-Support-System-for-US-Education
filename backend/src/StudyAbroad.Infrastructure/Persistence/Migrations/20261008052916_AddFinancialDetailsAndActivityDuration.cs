using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialDetailsAndActivityDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "financial_notes",
                schema: "app",
                table: "student_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "max_expected_tuition_usd",
                schema: "app",
                table: "student_profiles",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

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
                name: "financial_notes",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "max_expected_tuition_usd",
                schema: "app",
                table: "student_profiles");

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
