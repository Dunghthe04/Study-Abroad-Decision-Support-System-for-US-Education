using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtracurricularScoreScale4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Đổi thang ngoại khóa 0–10 sang 0–4 (thang của thầy): điểm > 4 chắc chắn là thang cũ thì quy đổi (×0.4)
            // trước khi siết ràng buộc; điểm ≤ 4 giữ nguyên vì không phân biệt được thang
            migrationBuilder.DropCheckConstraint(
                name: "ck_student_profiles_extracurricular_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.Sql(
                "UPDATE app.student_profiles SET extracurricular_score = ROUND(extracurricular_score * 0.4, 2) " +
                "WHERE extracurricular_score > 4;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_student_profiles_extracurricular_score",
                schema: "app",
                table: "student_profiles",
                sql: "extracurricular_score IS NULL OR extracurricular_score BETWEEN 0 AND 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_student_profiles_extracurricular_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.AddCheckConstraint(
                name: "ck_student_profiles_extracurricular_score",
                schema: "app",
                table: "student_profiles",
                sql: "extracurricular_score IS NULL OR extracurricular_score BETWEEN 0 AND 10");
        }
    }
}
