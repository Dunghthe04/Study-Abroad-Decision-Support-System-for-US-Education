using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlmTrainingSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "slm_training_samples",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sample_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Loại mẫu, tương ứng một sheet của file nguồn."),
                    question = table.Column<string>(type: "text", nullable: false, comment: "Đầu vào: tin nhắn của học sinh, giữ nguyên văn."),
                    profile = table.Column<string>(type: "jsonb", nullable: true, comment: "Đầu vào: hồ sơ đã biết. Trống nếu chưa có."),
                    pending_question = table.Column<string>(type: "text", nullable: true, comment: "Đầu vào: câu hệ thống vừa hỏi trước tin nhắn này."),
                    intents = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]", comment: "Đầu ra: danh sách intent gồm type, span, entities."),
                    context = table.Column<string>(type: "jsonb", nullable: true, comment: "Đầu vào: ngữ cảnh lượt trước."),
                    split = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "train", comment: "Tập dùng cho model: train, val hoặc test."),
                    source_review = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "Nhãn phiên bản rà soát trong file nguồn. Chỉ để tham khảo."),
                    review_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft", comment: "Trạng thái duyệt: draft, approved, rejected hoặc needs_fix."),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Ghi chú của người duyệt."),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Người duyệt gần nhất. Xóa tài khoản thì để trống."),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Thời điểm duyệt gần nhất."),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Người tạo hoặc import dòng này. Xóa tài khoản thì để trống."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_slm_training_samples", x => x.id);
                    table.CheckConstraint("ck_slm_training_samples_review_status", "review_status IN ('draft','approved','rejected','needs_fix')");
                    table.CheckConstraint("ck_slm_training_samples_split", "split IN ('train','val','test')");
                    table.CheckConstraint("ck_slm_training_samples_type", "sample_type IN ('collect_profile','profile_review','scenario','knowledge','center_referral','out_of_scope')");
                    table.ForeignKey(
                        name: "fk_slm_training_samples_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_slm_training_samples_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Dữ liệu huấn luyện SLM: mỗi dòng là một tin nhắn của học sinh và nhãn intents mà model phải trả về. Chỉ dòng approved được xuất ra JSONL để train.");

            migrationBuilder.CreateIndex(
                name: "ix_slm_training_samples_created_at",
                schema: "app",
                table: "slm_training_samples",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_slm_training_samples_created_by_user_id",
                schema: "app",
                table: "slm_training_samples",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_slm_training_samples_review_status_sample_type",
                schema: "app",
                table: "slm_training_samples",
                columns: new[] { "review_status", "sample_type" });

            migrationBuilder.CreateIndex(
                name: "ix_slm_training_samples_reviewed_by_user_id",
                schema: "app",
                table: "slm_training_samples",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_slm_training_samples_split",
                schema: "app",
                table: "slm_training_samples",
                column: "split");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "slm_training_samples",
                schema: "app");
        }
    }
}
