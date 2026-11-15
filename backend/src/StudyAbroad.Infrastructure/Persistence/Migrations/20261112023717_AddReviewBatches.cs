using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "review_batches",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, comment: "Tên đợt do người tạo đặt."),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Admin tạo đợt. Xóa tài khoản thì để trống, đợt vẫn giữ."),
                    seed = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Hạt giống của phép bốc ngẫu nhiên: cùng seed, cùng khoảng ngày và cùng dữ liệu thì ra đúng mẫu đó."),
                    size_per_stratum = table.Column<int>(type: "integer", nullable: false, comment: "Số câu muốn bốc cho mỗi nhóm skill (số thực tế có thể ít hơn nếu nhóm không đủ câu)."),
                    sampled_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Đầu khoảng thời gian của các câu được xét (tính cả đầu này). Trống là không giới hạn."),
                    sampled_to_utc_exclusive = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Cuối khoảng thời gian của các câu được xét (không tính đầu này). Trống là không giới hạn."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_batches_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Đợt duyệt mẫu: một mẫu ngẫu nhiên các câu trả lời của hệ thống để người duyệt chấm đúng sai (đo tỷ lệ lỗi mà hàng đợi không thấy). Không chứa nội dung chat; danh sách câu nằm ở review_batch_items.");

            migrationBuilder.CreateTable(
                name: "review_batch_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_message_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Tin trả lời của hệ thống được duyệt (bảng chat_messages)."),
                    stratum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, comment: "Nhóm dùng để chia tầng khi bốc mẫu (ví dụ skill của lượt), để nhóm ít gặp vẫn có mặt trong mẫu."),
                    position = table.Column<int>(type: "integer", nullable: false, comment: "Thứ tự trong đợt, theo thứ tự bốc."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_batch_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_batch_items_chat_messages_chat_message_id",
                        column: x => x.chat_message_id,
                        principalSchema: "app",
                        principalTable: "chat_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_batch_items_review_batches_review_batch_id",
                        column: x => x.review_batch_id,
                        principalSchema: "app",
                        principalTable: "review_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Một câu trả lời nằm trong mẫu của một đợt. Danh sách được lưu cố định để mẫu không bị bốc lại. Xóa tin nhắn gốc (ví dụ học sinh xóa tài khoản) thì dòng này và các kết luận của nó cũng mất.");

            migrationBuilder.CreateTable(
                name: "review_batch_reviewers",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_batch_reviewers", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_batch_reviewers_review_batches_review_batch_id",
                        column: x => x.review_batch_id,
                        principalSchema: "app",
                        principalTable: "review_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_batch_reviewers_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Người được giao duyệt một đợt. Chỉ người trong danh sách mới xem được nội dung các câu của đợt (mỗi lần xem vẫn ghi nhật ký).");

            migrationBuilder.CreateTable(
                name: "review_verdicts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_batch_item_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Câu được chấm (bảng review_batch_items)."),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Người chấm. Xóa tài khoản người chấm thì mất các kết luận của người đó."),
                    label_verdict = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "Nhãn ý SLM gán có đúng không: correct hoặc wrong."),
                    correct_intent = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, comment: "Nhãn đúng khi label_verdict là wrong (một trong sáu loại ý của SLM); trống nếu người duyệt không chắc."),
                    answer_verdict = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "Câu trả lời của hệ thống: correct, wrong, missing_data (thiếu dữ liệu) hoặc out_of_scope (ngoài phạm vi)."),
                    cause = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true, comment: "Vì sao sai, chỉ khi answer_verdict là wrong: misread_intent, missing_knowledge, hallucination hoặc technical."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_verdicts", x => x.id);
                    table.CheckConstraint("ck_review_verdicts_answer_verdict", "answer_verdict IN ('correct', 'wrong', 'missing_data', 'out_of_scope')");
                    table.CheckConstraint("ck_review_verdicts_cause", "cause IS NULL OR cause IN ('misread_intent', 'missing_knowledge', 'hallucination', 'technical')");
                    table.CheckConstraint("ck_review_verdicts_cause_needs_wrong", "cause IS NULL OR answer_verdict = 'wrong'");
                    table.CheckConstraint("ck_review_verdicts_correct_intent", "correct_intent IS NULL OR label_verdict = 'wrong'");
                    table.CheckConstraint("ck_review_verdicts_label_verdict", "label_verdict IN ('correct', 'wrong')");
                    table.ForeignKey(
                        name: "fk_review_verdicts_review_batch_items_review_batch_item_id",
                        column: x => x.review_batch_item_id,
                        principalSchema: "app",
                        principalTable: "review_batch_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_verdicts_users_reviewer_user_id",
                        column: x => x.reviewer_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Kết luận của MỘT người duyệt về MỘT câu trong đợt: nhãn ý của SLM đúng không và câu trả lời đúng không. Mỗi câu có thể có nhiều người chấm độc lập (một dòng mỗi người). Không chứa nội dung chat.");

            migrationBuilder.CreateIndex(
                name: "ix_review_batch_items_chat_message_id",
                schema: "app",
                table: "review_batch_items",
                column: "chat_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_batch_items_review_batch_id_chat_message_id",
                schema: "app",
                table: "review_batch_items",
                columns: new[] { "review_batch_id", "chat_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_batch_items_review_batch_id_position",
                schema: "app",
                table: "review_batch_items",
                columns: new[] { "review_batch_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_review_batch_reviewers_review_batch_id_user_id",
                schema: "app",
                table: "review_batch_reviewers",
                columns: new[] { "review_batch_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_batch_reviewers_user_id",
                schema: "app",
                table: "review_batch_reviewers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_batches_created_at",
                schema: "app",
                table: "review_batches",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_review_batches_created_by_user_id",
                schema: "app",
                table: "review_batches",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_verdicts_review_batch_item_id_reviewer_user_id",
                schema: "app",
                table: "review_verdicts",
                columns: new[] { "review_batch_item_id", "reviewer_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_verdicts_reviewer_user_id",
                schema: "app",
                table: "review_verdicts",
                column: "reviewer_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_batch_reviewers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "review_verdicts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "review_batch_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "review_batches",
                schema: "app");
        }
    }
}
