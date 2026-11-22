using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSensitivityRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensitivity_runs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Admin chạy phân tích. Xóa tài khoản thì để trống, lần chạy vẫn giữ."),
                    algorithm_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, comment: "Phiên bản thuật toán lúc chạy. Hai lần chạy khác phiên bản thì không so trực tiếp được."),
                    profile_set_version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "Phiên bản bộ hồ sơ giả lập (v1...)."),
                    profile_set_seed = table.Column<int>(type: "integer", nullable: false, comment: "Seed đã dùng để sinh bộ hồ sơ."),
                    profile_count = table.Column<int>(type: "integer", nullable: false, comment: "Số hồ sơ giả lập đã chạy."),
                    profile_set_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "SHA-256 của bộ hồ sơ, để chắc bộ hồ sơ không bị sửa."),
                    school_count = table.Column<int>(type: "integer", nullable: false, comment: "Số trường đưa vào phân tích (đang hoạt động, bậc đại học)."),
                    school_data_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "SHA-256 của các số liệu trường mà thuật toán đọc. Khác nhau nghĩa là dữ liệu trường đã đổi giữa hai lần chạy."),
                    request_json = table.Column<string>(type: "jsonb", nullable: false, comment: "Yêu cầu: các tham số đã chọn, mức nhích (10%/20%), hai ngưỡng nhãn."),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false, comment: "Giá trị mọi tham số trong bộ đang chạy lúc phân tích (mốc để nhích)."),
                    result_json = table.Column<string>(type: "jsonb", nullable: false, comment: "Kết quả từng tham số: giá trị gốc, từng mức nhích, số cặp đổi nhóm, số chỗ top 4 đổi, nhãn."),
                    stable_count = table.Column<int>(type: "integer", nullable: false, comment: "Số tham số nhãn Ổn định."),
                    watch_count = table.Column<int>(type: "integer", nullable: false, comment: "Số tham số nhãn Cần theo dõi."),
                    sensitive_count = table.Column<int>(type: "integer", nullable: false, comment: "Số tham số nhãn Nhạy."),
                    duration_ms = table.Column<int>(type: "integer", nullable: false, comment: "Thời gian chạy (mili giây)."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sensitivity_runs", x => x.id);
                    table.CheckConstraint("ck_sensitivity_runs_counts", "profile_count > 0 AND school_count >= 0 AND stable_count >= 0 AND watch_count >= 0 AND sensitive_count >= 0 AND duration_ms >= 0");
                    table.ForeignKey(
                        name: "fk_sensitivity_runs_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "[UC-85] Mỗi lần admin phân tích độ nhạy tham số của thuật toán gợi ý trường. Chỉ lưu khi chạy xong trọn vẹn. Chạy trên bộ hồ sơ giả lập, không có dữ liệu học sinh thật.");

            migrationBuilder.CreateIndex(
                name: "ix_sensitivity_runs_created_at",
                schema: "app",
                table: "sensitivity_runs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_sensitivity_runs_created_by_user_id",
                schema: "app",
                table: "sensitivity_runs",
                column: "created_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensitivity_runs",
                schema: "app");
        }
    }
}
