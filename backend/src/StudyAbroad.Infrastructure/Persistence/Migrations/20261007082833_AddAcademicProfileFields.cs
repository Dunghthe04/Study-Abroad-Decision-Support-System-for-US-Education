using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "raw_score",
                schema: "app",
                table: "transcript_scores",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "education_system",
                schema: "app",
                table: "student_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "graduation_year",
                schema: "app",
                table: "student_profiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "raw_score",
                schema: "app",
                table: "transcript_scores");

            migrationBuilder.DropColumn(
                name: "education_system",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "graduation_year",
                schema: "app",
                table: "student_profiles");
        }
    }
}
