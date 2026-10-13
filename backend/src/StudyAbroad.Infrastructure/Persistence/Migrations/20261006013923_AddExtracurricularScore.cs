using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtracurricularScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                sql: "extracurricular_score IS NULL OR extracurricular_score BETWEEN 0 AND 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_student_profiles_extracurricular_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "extracurricular_score",
                schema: "app",
                table: "student_profiles");
        }
    }
}
