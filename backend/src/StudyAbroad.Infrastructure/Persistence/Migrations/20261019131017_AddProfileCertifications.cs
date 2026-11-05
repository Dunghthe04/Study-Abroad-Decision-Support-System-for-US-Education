using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileCertifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "english_planned_date",
                schema: "app",
                table: "student_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "english_test_planned",
                schema: "app",
                table: "student_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "profile_certifications",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    test_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    custom_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    overall_score = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    scale_text = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    section_scores_json = table.Column<string>(type: "jsonb", nullable: true),
                    test_date = table.Column<DateOnly>(type: "date", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_certifications", x => x.id);
                    table.CheckConstraint("ck_profile_certifications_other_name", "test_type <> 'other' OR custom_name IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_profile_certifications_student_profiles_student_profile_id",
                        column: x => x.student_profile_id,
                        principalSchema: "app",
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_profile_certifications_student_profile_id_test_type",
                schema: "app",
                table: "profile_certifications",
                columns: new[] { "student_profile_id", "test_type" });

            // Chuyển điểm ở các cột cũ sang profile_certifications (mỗi cột có điểm → 1 dòng). Cột cũ giữ nguyên
            // vì gợi ý trường vẫn đọc chúng; ngày thi chỉ biết với chứng chỉ tiếng Anh đang chọn (english_test_type)
            migrationBuilder.Sql("""
                INSERT INTO app.profile_certifications
                    (id, student_profile_id, category, test_type, overall_score, test_date, sort_order, created_at)
                SELECT gen_random_uuid(), p.id, v.category, v.test_type, v.score,
                       CASE WHEN v.category = 'english' AND lower(p.english_test_type) = v.test_type
                            THEN p.english_test_date END,
                       v.sort_order, now()
                FROM app.student_profiles p
                CROSS JOIN LATERAL (VALUES
                    ('english', 'ielts', p.ielts, 0),
                    ('english', 'toefl', p.toefl, 1),
                    ('english', 'duolingo', p.duolingo, 2),
                    ('standardized', 'sat', p.sat, 3),
                    ('standardized', 'act', p.act, 4),
                    ('standardized', 'ap',
                        CASE WHEN jsonb_typeof(p.other_tests_json -> 'ap') = 'number'
                             THEN (p.other_tests_json ->> 'ap')::numeric END, 5),
                    ('graduate', 'gre', p.gre, 6),
                    ('graduate', 'gmat', p.gmat, 7)
                ) AS v(category, test_type, score, sort_order)
                WHERE v.score IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "profile_certifications",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "english_planned_date",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "english_test_planned",
                schema: "app",
                table: "student_profiles");
        }
    }
}
