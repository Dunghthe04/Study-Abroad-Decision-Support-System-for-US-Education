using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceExtracurricularScoreWithTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_student_profiles_extracurricular_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "extracurricular_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.AddColumn<int>(
                name: "extracurricular_tier",
                schema: "app",
                table: "student_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_student_profiles_extracurricular_tier",
                schema: "app",
                table: "student_profiles",
                sql: "extracurricular_tier IS NULL OR extracurricular_tier BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_student_profiles_extracurricular_tier",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "extracurricular_tier",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.AddColumn<decimal>(
                name: "extracurricular_score",
                schema: "app",
                table: "student_profiles",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_student_profiles_extracurricular_score",
                schema: "app",
                table: "student_profiles",
                sql: "extracurricular_score IS NULL OR extracurricular_score BETWEEN 0 AND 4");
        }
    }
}
