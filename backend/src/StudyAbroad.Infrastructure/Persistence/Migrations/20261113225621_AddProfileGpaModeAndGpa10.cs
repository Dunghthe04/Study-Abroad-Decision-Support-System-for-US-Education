using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileGpaModeAndGpa10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gpa_mode",
                schema: "app",
                table: "student_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "overall_gpa10",
                schema: "app",
                table: "student_profiles",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gpa_mode",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "overall_gpa10",
                schema: "app",
                table: "student_profiles");
        }
    }
}
