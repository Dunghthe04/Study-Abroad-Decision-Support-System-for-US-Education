using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// [USAS-640] Lưu đủ dữ liệu luồng hồ sơ 6 bước.
    /// Viết bằng SQL idempotent vì một số DB local đã được thêm tay budget_min_usd / budget_max_usd / target_school_type
    /// (migration viết tay trước đó không có Designer nên EF không áp dụng).
    /// </summary>
    public partial class ProfileFlowSixStepStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // student_profiles: ngân sách theo mức (thay annual_budget_usd)
            migrationBuilder.Sql("""
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS budget_min_usd numeric(12,2);
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS budget_max_usd numeric(12,2);
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS budget_tier character varying(32);
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS target_school_type character varying(32) NOT NULL DEFAULT 'all';
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS scholarship_need character varying(16);
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS current_education_level character varying(32);
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS english_test_type character varying(16);
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS english_test_date date;
                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS flow_submitted_at timestamp with time zone;
                """);

            // Chuyển annual_budget_usd cũ sang budget_tier + min/max rồi bỏ cột cũ
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_schema = 'app' AND table_name = 'student_profiles' AND column_name = 'annual_budget_usd') THEN
                        UPDATE app.student_profiles
                        SET budget_max_usd = COALESCE(budget_max_usd, annual_budget_usd)
                        WHERE annual_budget_usd IS NOT NULL;
                        ALTER TABLE app.student_profiles DROP COLUMN annual_budget_usd;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE app.student_profiles
                SET budget_tier = CASE
                        WHEN budget_max_usd <= 20000 THEN 'tier_under_20k'
                        WHEN budget_max_usd <= 35000 THEN 'tier_20k_35k'
                        WHEN budget_max_usd <= 50000 THEN 'tier_35k_50k'
                        WHEN budget_max_usd <= 70000 THEN 'tier_50k_70k'
                        ELSE 'tier_above_70k'
                    END
                WHERE budget_tier IS NULL AND budget_max_usd IS NOT NULL;

                UPDATE app.student_profiles
                SET budget_min_usd = CASE budget_tier
                        WHEN 'tier_under_20k' THEN 0
                        WHEN 'tier_20k_35k' THEN 20000
                        WHEN 'tier_35k_50k' THEN 35000
                        WHEN 'tier_50k_70k' THEN 50000
                        ELSE 70000
                    END,
                    budget_max_usd = CASE budget_tier
                        WHEN 'tier_under_20k' THEN 20000
                        WHEN 'tier_20k_35k' THEN 35000
                        WHEN 'tier_35k_50k' THEN 50000
                        WHEN 'tier_50k_70k' THEN 70000
                        ELSE NULL
                    END
                WHERE budget_tier IS NOT NULL;
                """);

            // Nhu cầu học bổng: bool cũ chỉ phân biệt được có/không; chỉ điền cho hồ sơ đã khai tài chính
            migrationBuilder.Sql("""
                UPDATE app.student_profiles
                SET scholarship_need = CASE WHEN needs_scholarship THEN 'preferred' ELSE 'optional' END
                WHERE scholarship_need IS NULL AND (needs_scholarship OR budget_tier IS NOT NULL);
                """);

            // Cấp học hiện tại: luồng cũ ghi nhãn tiếng Việt vào education_system
            migrationBuilder.Sql("""
                UPDATE app.student_profiles
                SET current_education_level = CASE education_system
                        WHEN 'Trung học cơ sở' THEN 'middle_school'
                        WHEN 'Trung học phổ thông' THEN 'high_school'
                        WHEN 'Cao đẳng / Đại học' THEN 'college_university'
                        WHEN 'Thạc sĩ' THEN 'master'
                    END,
                    education_system = NULL
                WHERE current_education_level IS NULL
                  AND education_system IN ('Trung học cơ sở', 'Trung học phổ thông', 'Cao đẳng / Đại học', 'Thạc sĩ');
                """);

            // transcript_scores: tách khối + học kỳ khỏi term_name
            migrationBuilder.Sql("""
                ALTER TABLE app.transcript_scores ADD COLUMN IF NOT EXISTS grade_name character varying(32);
                ALTER TABLE app.transcript_scores ADD COLUMN IF NOT EXISTS semester integer;

                UPDATE app.transcript_scores
                SET grade_name = btrim(regexp_replace(substring(term_name from '^(.*?)\s*-?\s*HK\s*[12]\s*$'), '\s*-\s*$', '')),
                    semester = substring(term_name from 'HK\s*([12])\s*$')::int
                WHERE semester IS NULL AND term_name ~ 'HK\s*[12]\s*$';
                """);

            // profile_activities: năm đạt giải + ràng buộc phạm vi 1..5
            migrationBuilder.Sql("""
                ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS award_year integer;

                UPDATE app.profile_activities
                SET award_year = EXTRACT(YEAR FROM start_date)::int
                WHERE kind = 'award' AND award_year IS NULL AND start_date IS NOT NULL;

                ALTER TABLE app.profile_activities DROP CONSTRAINT IF EXISTS ck_profile_activities_impact_level;
                ALTER TABLE app.profile_activities ADD CONSTRAINT ck_profile_activities_impact_level
                    CHECK (impact_level IS NULL OR impact_level BETWEEN 1 AND 5);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE app.profile_activities DROP CONSTRAINT IF EXISTS ck_profile_activities_impact_level;
                ALTER TABLE app.profile_activities DROP COLUMN IF EXISTS award_year;
                ALTER TABLE app.transcript_scores DROP COLUMN IF EXISTS grade_name;
                ALTER TABLE app.transcript_scores DROP COLUMN IF EXISTS semester;

                ALTER TABLE app.student_profiles ADD COLUMN IF NOT EXISTS annual_budget_usd numeric(12,2);
                UPDATE app.student_profiles SET annual_budget_usd = COALESCE(budget_max_usd, budget_min_usd);

                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS budget_min_usd;
                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS budget_max_usd;
                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS budget_tier;
                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS target_school_type;
                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS scholarship_need;
                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS current_education_level;
                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS english_test_type;
                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS english_test_date;
                ALTER TABLE app.student_profiles DROP COLUMN IF EXISTS flow_submitted_at;
                """);
        }
    }
}
