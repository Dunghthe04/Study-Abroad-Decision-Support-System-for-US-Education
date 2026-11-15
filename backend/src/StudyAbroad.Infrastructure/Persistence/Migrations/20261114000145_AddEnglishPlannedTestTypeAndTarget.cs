using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnglishPlannedTestTypeAndTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "english_planned_target_score",
                schema: "app",
                table: "student_profiles",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "english_planned_test_type",
                schema: "app",
                table: "student_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "english_planned_target_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "english_planned_test_type",
                schema: "app",
                table: "student_profiles");
        }
    }
}
