using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignCodeWithUcSpec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_slm_training_samples_users_reviewed_by_user_id",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropIndex(
                name: "ix_slm_training_samples_review_status_sample_type",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropIndex(
                name: "ix_slm_training_samples_reviewed_by_user_id",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropCheckConstraint(
                name: "ck_slm_training_samples_review_status",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "review_note",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "review_status",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "reviewed_by_user_id",
                schema: "app",
                table: "slm_training_samples");
            migrationBuilder.DropColumn(
                name: "image_url",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "media_json",
                schema: "app",
                table: "chat_messages");

            migrationBuilder.AlterTable(
                name: "slm_training_samples",
                schema: "app",
                comment: "Dữ liệu huấn luyện SLM: mỗi dòng là một tin nhắn của học sinh và nhãn intents mà model phải trả về. Mọi mẫu chưa xóa được xuất ra JSONL/Excel để train (UC-96).",
                oldComment: "Dữ liệu huấn luyện SLM: mỗi dòng là một tin nhắn của học sinh và nhãn intents mà model phải trả về. Chỉ dòng approved được xuất ra JSONL để train.");

            migrationBuilder.AlterTable(
                name: "knowledge_documents",
                schema: "app",
                comment: "Kho tri thức cho chat tư vấn: mỗi dòng là một cặp câu hỏi mẫu và câu trả lời đã chốt, nhập từ file Excel (sheet knowledge_base, 10 cột). Chat chỉ dùng dòng chưa xóa mềm.",
                oldComment: "Kho tri thức cho chat tư vấn: mỗi dòng là một cặp câu hỏi mẫu và câu trả lời đã chốt, nhập từ file Excel (sheet knowledge_base). Chat so vector của câu hỏi học sinh với vector của question để tìm câu trả lời.");

            migrationBuilder.AlterColumn<DateTime>(
                name: "exported_at",
                schema: "app",
                table: "slm_training_samples",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Lần xuất gần nhất (UC-96).",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Lần xuất gần nhất.");

            migrationBuilder.AlterColumn<DateTime>(
                name: "deleted_at",
                schema: "app",
                table: "slm_training_samples",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Xóa mềm: khác null thì không hiện ở Đang dùng và không xuất.",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Xóa mềm: khác null thì không hiện, không xuất.");

            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "app",
                table: "slm_training_samples",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                comment: "Phiên bản hiện tại của mẫu.");

            migrationBuilder.AddColumn<decimal>(
                name: "min_duolingo",
                schema: "app",
                table: "scholarships",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_buyer_retention_id",
                schema: "app",
                table: "payment_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "received_amount_vnd",
                schema: "app",
                table: "payment_transactions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "note",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                comment: "Ghi chú nội bộ (tự do khi nhập tay; trong Excel dòng có note bắt đầu bằng CHỜ DUYỆT được báo lỗi dòng).",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Ghi chú nội bộ cho người thu thập. Không đưa cho học sinh. Note bắt đầu bằng CHỜ DUYỆT thì dòng chưa chốt, import bỏ qua.");

            migrationBuilder.AlterColumn<int>(
                name: "entry_no",
                schema: "app",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                comment: "Cột id trong file Excel, khóa để cập nhật khi nhập lại file. Không trùng và không cấp lại khi xóa mềm.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Cột id trong file Excel, khóa để cập nhật khi nhập lại file. Không trùng.");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                schema: "app",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Xóa mềm (UC-116): khác null thì chat không dùng.");

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                schema: "app",
                table: "knowledge_documents",
                type: "uuid",
                nullable: true,
                comment: "Người xóa mềm.");

            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "app",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                comment: "Phiên bản hiện tại của mục tri thức.");

            migrationBuilder.AlterColumn<string>(
                name: "sources_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                comment: "Nguồn trích dẫn của câu trả lời (title, url, retrievedAt, entryNo, version).",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "Nguồn trích dẫn của câu trả lời (title, url).");

            migrationBuilder.CreateTable(
                name: "deleted_buyer_retentions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    retain_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deleted_buyer_retentions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_document_versions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    restored_from_version = table.Column<int>(type: "integer", nullable: true),
                    doc_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    answer = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    retrieved_at = table.Column<DateOnly>(type: "date", nullable: false),
                    topic_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_document_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_document_versions_knowledge_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "app",
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_knowledge_document_versions_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Lịch sử phiên bản bất biến của mục kho tri thức (UC-113).");

            migrationBuilder.CreateTable(
                name: "slm_sample_versions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sample_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    restored_from_version = table.Column<int>(type: "integer", nullable: true),
                    sample_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    profile = table.Column<string>(type: "jsonb", nullable: true),
                    pending_question = table.Column<string>(type: "text", nullable: true),
                    intents = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    context = table.Column<string>(type: "jsonb", nullable: true),
                    split = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    source_review = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    source = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_slm_sample_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_slm_sample_versions_slm_training_samples_sample_id",
                        column: x => x.sample_id,
                        principalSchema: "app",
                        principalTable: "slm_training_samples",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_slm_sample_versions_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Lịch sử phiên bản bất biến của mẫu huấn luyện SLM (UC-90).");

            migrationBuilder.CreateIndex(
                name: "ix_slm_training_samples_deleted_at",
                schema: "app",
                table: "slm_training_samples",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_slm_training_samples_sample_type",
                schema: "app",
                table: "slm_training_samples",
                column: "sample_type");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_deleted_buyer_retention_id",
                schema: "app",
                table: "payment_transactions",
                column: "deleted_buyer_retention_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_deleted_at",
                schema: "app",
                table: "knowledge_documents",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_deleted_by_user_id",
                schema: "app",
                table: "knowledge_documents",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_deleted_buyer_retentions_buyer_code",
                schema: "app",
                table: "deleted_buyer_retentions",
                column: "buyer_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deleted_buyer_retentions_retain_until_utc",
                schema: "app",
                table: "deleted_buyer_retentions",
                column: "retain_until_utc");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_document_versions_actor_user_id",
                schema: "app",
                table: "knowledge_document_versions",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_document_versions_document_id_created_at",
                schema: "app",
                table: "knowledge_document_versions",
                columns: new[] { "document_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_document_versions_document_id_version",
                schema: "app",
                table: "knowledge_document_versions",
                columns: new[] { "document_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_slm_sample_versions_actor_user_id",
                schema: "app",
                table: "slm_sample_versions",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_slm_sample_versions_sample_id_created_at",
                schema: "app",
                table: "slm_sample_versions",
                columns: new[] { "sample_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_slm_sample_versions_sample_id_version",
                schema: "app",
                table: "slm_sample_versions",
                columns: new[] { "sample_id", "version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_documents_users_deleted_by_user_id",
                schema: "app",
                table: "knowledge_documents",
                column: "deleted_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_transactions_deleted_buyer_retentions_deleted_buyer",
                schema: "app",
                table: "payment_transactions",
                column: "deleted_buyer_retention_id",
                principalSchema: "app",
                principalTable: "deleted_buyer_retentions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: every existing row becomes immutable version v1 (UC-90, UC-113).
            migrationBuilder.Sql(@"
INSERT INTO app.slm_sample_versions (id, sample_id, version, actor_user_id, action, restored_from_version,
    sample_type, question, profile, pending_question, intents, context, split, source_review, source, created_at)
SELECT gen_random_uuid(), s.id, 1, s.created_by_user_id, 'create', NULL,
    s.sample_type, s.question, s.profile, s.pending_question, s.intents, s.context, s.split, s.source_review, s.source, s.created_at
FROM app.slm_training_samples s;

INSERT INTO app.knowledge_document_versions (id, document_id, version, actor_user_id, action, restored_from_version,
    doc_type, label, question, answer, source, source_url, retrieved_at, topic_code, note, created_at)
SELECT gen_random_uuid(), d.id, 1, NULL, 'create', NULL,
    d.doc_type, d.label, d.question, d.answer, d.source, d.source_url, d.retrieved_at, d.topic_code, d.note, d.created_at
FROM app.knowledge_documents d;

-- UC-125: received amount = what payOS reported in the stored webhook payload.
UPDATE app.payment_transactions SET received_amount_vnd = (payload_json::jsonb->>'amount')::numeric
WHERE status IN ('paid', 'mismatch') AND received_amount_vnd IS NULL AND payload_json LIKE '{%'
  AND jsonb_typeof(payload_json::jsonb->'amount') = 'number';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_documents_users_deleted_by_user_id",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropForeignKey(
                name: "fk_payment_transactions_deleted_buyer_retentions_deleted_buyer",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropTable(
                name: "deleted_buyer_retentions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "knowledge_document_versions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "slm_sample_versions",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_slm_training_samples_deleted_at",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropIndex(
                name: "ix_slm_training_samples_sample_type",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropIndex(
                name: "ix_payment_transactions_deleted_buyer_retention_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_deleted_at",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_deleted_by_user_id",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "min_duolingo",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "deleted_buyer_retention_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "received_amount_vnd",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.AlterTable(
                name: "slm_training_samples",
                schema: "app",
                comment: "Dữ liệu huấn luyện SLM: mỗi dòng là một tin nhắn của học sinh và nhãn intents mà model phải trả về. Chỉ dòng approved được xuất ra JSONL để train.",
                oldComment: "Dữ liệu huấn luyện SLM: mỗi dòng là một tin nhắn của học sinh và nhãn intents mà model phải trả về. Mọi mẫu chưa xóa được xuất ra JSONL/Excel để train (UC-96).");

            migrationBuilder.AlterTable(
                name: "knowledge_documents",
                schema: "app",
                comment: "Kho tri thức cho chat tư vấn: mỗi dòng là một cặp câu hỏi mẫu và câu trả lời đã chốt, nhập từ file Excel (sheet knowledge_base). Chat so vector của câu hỏi học sinh với vector của question để tìm câu trả lời.",
                oldComment: "Kho tri thức cho chat tư vấn: mỗi dòng là một cặp câu hỏi mẫu và câu trả lời đã chốt, nhập từ file Excel (sheet knowledge_base, 10 cột). Chat chỉ dùng dòng chưa xóa mềm.");

            migrationBuilder.AlterColumn<DateTime>(
                name: "exported_at",
                schema: "app",
                table: "slm_training_samples",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Lần xuất gần nhất.",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Lần xuất gần nhất (UC-96).");

            migrationBuilder.AlterColumn<DateTime>(
                name: "deleted_at",
                schema: "app",
                table: "slm_training_samples",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Xóa mềm: khác null thì không hiện, không xuất.",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Xóa mềm: khác null thì không hiện ở Đang dùng và không xuất.");

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                schema: "app",
                table: "slm_training_samples",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "Ghi chú của người duyệt.");

            migrationBuilder.AddColumn<string>(
                name: "review_status",
                schema: "app",
                table: "slm_training_samples",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                comment: "Trạng thái duyệt: draft, pending, approved, rejected hoặc needs_fix.");

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                schema: "app",
                table: "slm_training_samples",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Thời điểm duyệt gần nhất.");

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_user_id",
                schema: "app",
                table: "slm_training_samples",
                type: "uuid",
                nullable: true,
                comment: "Người duyệt gần nhất. Xóa tài khoản thì để trống.");

            migrationBuilder.AlterColumn<string>(
                name: "note",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                comment: "Ghi chú nội bộ cho người thu thập. Không đưa cho học sinh. Note bắt đầu bằng CHỜ DUYỆT thì dòng chưa chốt, import bỏ qua.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Ghi chú nội bộ (tự do khi nhập tay; trong Excel dòng có note bắt đầu bằng CHỜ DUYỆT được báo lỗi dòng).");

            migrationBuilder.AlterColumn<int>(
                name: "entry_no",
                schema: "app",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                comment: "Cột id trong file Excel, khóa để cập nhật khi nhập lại file. Không trùng.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Cột id trong file Excel, khóa để cập nhật khi nhập lại file. Không trùng và không cấp lại khi xóa mềm.");

            migrationBuilder.AlterColumn<string>(
                name: "sources_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                comment: "Nguồn trích dẫn của câu trả lời (title, url).",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "Nguồn trích dẫn của câu trả lời (title, url, retrievedAt, entryNo, version).");

            migrationBuilder.AddColumn<string>(
                name: "media_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                comment: "Ảnh minh họa của câu trả lời: [{kind, url, alt}]. Link https tới ảnh, lấy từ cột image_url của dòng kho tri thức được dùng; trình duyệt tải ảnh thẳng từ link. Null nếu không có ảnh.");

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_slm_training_samples_review_status",
                schema: "app",
                table: "slm_training_samples",
                sql: "review_status IN ('draft','pending','approved','rejected','needs_fix')");

            migrationBuilder.AddForeignKey(
                name: "fk_slm_training_samples_users_reviewed_by_user_id",
                schema: "app",
                table: "slm_training_samples",
                column: "reviewed_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
