using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyTestColumnsFromStudentProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Điểm thi giờ chỉ đọc từ profile_certifications. Hồ sơ cũ chưa có dòng chứng chỉ nào mà còn điểm trên cột thì chép
            // thành dòng đã thi trước khi xóa cột (như form từng dựng lại từ cột cũ). Mục đang tick "chưa thi" thì điểm trên cột
            // là điểm kỳ vọng nên bỏ qua
            migrationBuilder.Sql("""
                INSERT INTO app.profile_certifications
                    (id, student_profile_id, category, test_type, overall_score, test_date, is_target, sort_order, created_at)
                SELECT gen_random_uuid(), x.profile_id, x.category, x.test_type, x.score, x.test_date, false,
                       row_number() OVER (PARTITION BY x.profile_id ORDER BY x.ord) - 1, now()
                FROM (
                    SELECT p.id AS profile_id, v.category, v.test_type, v.score, v.ord,
                           CASE WHEN lower(p.english_test_type) = v.test_type THEN p.english_test_date END AS test_date,
                           CASE WHEN v.category = 'english' THEN p.english_test_planned ELSE p.standardized_test_planned END AS planned
                    FROM app.student_profiles p
                    CROSS JOIN LATERAL (VALUES
                        ('english', 'ielts', p.ielts, 1),
                        ('english', 'toefl', p.toefl, 2),
                        ('english', 'duolingo', p.duolingo, 3),
                        ('standardized', 'sat', p.sat, 4),
                        ('standardized', 'act', p.act, 5),
                        ('standardized', 'ap',
                            CASE WHEN jsonb_typeof(p.other_tests_json -> 'ap') = 'number'
                                 THEN (p.other_tests_json ->> 'ap')::numeric END, 6),
                        ('graduate', 'gre', p.gre, 7),
                        ('graduate', 'gmat', p.gmat, 8)
                    ) AS v(category, test_type, score, ord)
                    WHERE NOT EXISTS (SELECT 1 FROM app.profile_certifications c WHERE c.student_profile_id = p.id)
                ) x
                WHERE x.score IS NOT NULL AND NOT x.planned;
                """);

            migrationBuilder.DropColumn(
                name: "act",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "duolingo",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "english_test_date",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "english_test_type",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "gmat",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "gre",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "ielts",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "other_tests_json",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "roadmap_progress_json",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "sat",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "toefl",
                schema: "app",
                table: "student_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "act",
                schema: "app",
                table: "student_profiles",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "duolingo",
                schema: "app",
                table: "student_profiles",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "english_test_date",
                schema: "app",
                table: "student_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "english_test_type",
                schema: "app",
                table: "student_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gmat",
                schema: "app",
                table: "student_profiles",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gre",
                schema: "app",
                table: "student_profiles",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ielts",
                schema: "app",
                table: "student_profiles",
                type: "numeric(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "other_tests_json",
                schema: "app",
                table: "student_profiles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "roadmap_progress_json",
                schema: "app",
                table: "student_profiles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sat",
                schema: "app",
                table: "student_profiles",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "toefl",
                schema: "app",
                table: "student_profiles",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);
        }
    }
}
