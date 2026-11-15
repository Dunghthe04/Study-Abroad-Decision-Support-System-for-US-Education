using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovePlannedTestsToCertifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mỗi dự định thi cũ (1 mỗi mục, trên cột hồ sơ) thành 1 dòng mục tiêu (is_target) trong profile_certifications.
            // Loại bài: cột kế hoạch; hồ sơ lưu trước khi có cột đó thì loại ở english_test_type, điểm mục tiêu ở cột điểm của loại đó.
            // Dự định chưa chọn loại bài thì không có gì để chép: tick giữ nguyên, nghĩa mới là "chưa thi và chưa có dự định thi"
            migrationBuilder.Sql("""
                CREATE TEMP TABLE planned_tests AS
                SELECT p.id AS profile_id, 'english' AS category,
                       lower(COALESCE(p.english_planned_test_type, NULLIF(lower(p.english_test_type), 'none'))) AS test_type,
                       CASE WHEN p.english_planned_test_type IS NOT NULL OR p.english_planned_target_score IS NOT NULL
                            THEN p.english_planned_target_score
                            ELSE CASE lower(p.english_test_type)
                                     WHEN 'ielts' THEN p.ielts WHEN 'toefl' THEN p.toefl WHEN 'duolingo' THEN p.duolingo END
                       END AS score,
                       p.english_planned_date AS test_date
                FROM app.student_profiles p
                WHERE p.english_test_planned
                UNION ALL
                SELECT p.id,
                       CASE WHEN lower(p.standardized_planned_test_type) IN ('gre', 'gmat') THEN 'graduate' ELSE 'standardized' END,
                       lower(p.standardized_planned_test_type), p.standardized_planned_target_score, p.standardized_planned_date
                FROM app.student_profiles p
                WHERE p.standardized_test_planned;

                DELETE FROM planned_tests WHERE test_type IS NULL OR test_type = '';

                -- Migration AddProfileCertifications chép cả điểm mục tiêu trên cột thành dòng "đã thi": dòng cùng loại, cùng điểm
                -- đó chính là dự định nên đổi thành dòng mục tiêu, không thêm dòng mới
                UPDATE app.profile_certifications c
                SET is_target = true, test_date = pt.test_date, updated_at = now()
                FROM planned_tests pt
                WHERE c.student_profile_id = pt.profile_id AND c.test_type = pt.test_type
                  AND NOT c.is_target AND c.overall_score IS NOT DISTINCT FROM pt.score
                  AND NOT EXISTS (SELECT 1 FROM app.profile_certifications t
                                  WHERE t.student_profile_id = pt.profile_id AND t.test_type = pt.test_type AND t.is_target);

                INSERT INTO app.profile_certifications
                    (id, student_profile_id, category, test_type, overall_score, test_date, is_target, sort_order, created_at)
                SELECT gen_random_uuid(), pt.profile_id, pt.category, pt.test_type, pt.score, pt.test_date, true,
                       COALESCE((SELECT max(c.sort_order) FROM app.profile_certifications c
                                 WHERE c.student_profile_id = pt.profile_id), -1)
                           + CASE pt.category WHEN 'english' THEN 1 ELSE 2 END,
                       now()
                FROM planned_tests pt
                WHERE NOT EXISTS (SELECT 1 FROM app.profile_certifications t
                                  WHERE t.student_profile_id = pt.profile_id AND t.test_type = pt.test_type AND t.is_target);

                -- Tick giờ là "chưa thi và chưa có dự định thi": mục đã có chứng chỉ hoặc dự định thì bỏ tick
                UPDATE app.student_profiles p SET english_test_planned = false
                WHERE p.english_test_planned AND EXISTS (SELECT 1 FROM app.profile_certifications c
                    WHERE c.student_profile_id = p.id AND c.category = 'english');
                UPDATE app.student_profiles p SET standardized_test_planned = false
                WHERE p.standardized_test_planned AND EXISTS (SELECT 1 FROM app.profile_certifications c
                    WHERE c.student_profile_id = p.id AND c.category <> 'english');

                DROP TABLE planned_tests;
                """);

            migrationBuilder.DropColumn(
                name: "english_planned_date",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "english_planned_target_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "english_planned_test_type",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "standardized_planned_date",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "standardized_planned_target_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "standardized_planned_test_type",
                schema: "app",
                table: "student_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "english_planned_date",
                schema: "app",
                table: "student_profiles",
                type: "date",
                nullable: true);

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

            migrationBuilder.AddColumn<DateOnly>(
                name: "standardized_planned_date",
                schema: "app",
                table: "student_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "standardized_planned_target_score",
                schema: "app",
                table: "student_profiles",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "standardized_planned_test_type",
                schema: "app",
                table: "student_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }
    }
}
