using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// [UC-80] payment_transactions.user_id và forum_reports.reporter_user_id cho phép null để giữ dữ liệu ẩn danh
    /// khi xóa hẳn tài khoản. Trigger này chặn null lúc THÊM MỚI (quên gán người dùng là lỗi);
    /// null do xóa user (FK set null) là UPDATE nên không bị chặn.
    /// </summary>
    public partial class RequireUserOnInsertPaymentsAndReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Một hàm dùng chung, tên cột truyền qua tham số trigger
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION app.require_user_on_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF to_jsonb(NEW) ->> TG_ARGV[0] IS NULL THEN
                        RAISE EXCEPTION '%.% must not be null on insert', TG_TABLE_NAME, TG_ARGV[0]
                            USING ERRCODE = 'not_null_violation';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_payment_transactions_require_user
                BEFORE INSERT ON app.payment_transactions
                FOR EACH ROW EXECUTE FUNCTION app.require_user_on_insert('user_id');
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_forum_reports_require_reporter
                BEFORE INSERT ON app.forum_reports
                FOR EACH ROW EXECUTE FUNCTION app.require_user_on_insert('reporter_user_id');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_forum_reports_require_reporter ON app.forum_reports;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_payment_transactions_require_user ON app.payment_transactions;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS app.require_user_on_insert();");
        }
    }
}
