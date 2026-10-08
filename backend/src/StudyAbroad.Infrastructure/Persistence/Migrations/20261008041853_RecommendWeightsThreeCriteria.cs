using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ghi cấu hình gợi ý trường dạng mới vào app_settings (key recommend.weights) để admin xem và sửa:
    /// SAW 3 tiêu chí (học thuật 0.5, tài chính 0.3, ngoại khóa 0.2), trong học thuật GPA 0.4, SAT 0.4, tiếng Anh 0.2.
    /// Dòng cũ (financeWeight, extracurricularWeight) code không còn đọc nên ghi đè.
    /// </summary>
    public partial class RecommendWeightsThreeCriteria : Migration
    {
        private const string Settings = """
            {"weights": {"academic": 0.5, "finance": 0.3, "extracurricular": 0.2},
             "academicParts": {"gpa": 0.4, "sat": 0.4, "english": 0.2},
             "gpaBand": 0.3, "budgetTolerance": 0.10, "missingValue": 0.5,
             "maxResults": 12, "perCategory": {"reach": 3, "match": 5, "safety": 4},
             "openAdmissionLevels": ["community_college"],
             "aiEnabled": true, "aiTimeoutSeconds": 120, "extracurricularTimeoutSeconds": 60}
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO app.app_settings (id, key, value_json, description, created_at)
                VALUES (gen_random_uuid(), 'recommend.weights', '{Settings}'::jsonb,
                        'Gợi ý trường: trọng số SAW 3 tiêu chí (AHP), trọng số trong học thuật, ngưỡng và giới hạn', now())
                ON CONFLICT (key) DO UPDATE SET value_json = EXCLUDED.value_json,
                    description = EXCLUDED.description, updated_at = now();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không khôi phục dạng cũ: code không còn đọc được dạng đó, xóa dòng thì dùng giá trị mặc định trong code
            migrationBuilder.Sql("DELETE FROM app.app_settings WHERE key = 'recommend.weights';");
        }
    }
}
