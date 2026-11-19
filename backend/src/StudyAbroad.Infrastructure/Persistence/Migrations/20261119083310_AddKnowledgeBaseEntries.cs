using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBaseEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_base_entries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_no = table.Column<int>(type: "integer", nullable: false, comment: "Mã số ổn định, trùng cột id của file Excel. Thêm tay thì database cấp số tiếp theo.")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "Nhãn chủ đề LABEL_...; nháp có thể trống."),
                    levels = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'", comment: "Mã bậc học: chung, thcs, thpt, dai_hoc, thac_si, tien_si."),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Câu hỏi."),
                    answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true, comment: "Câu trả lời đã kiểm chứng; nháp có thể trống."),
                    source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "Trích dẫn nguồn, giữ nguyên văn nhiều dòng."),
                    origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "manual", comment: "Dòng đến từ đâu: manual, excel hoặc chat-unanswered."),
                    review_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft", comment: "Trạng thái duyệt: draft, pending, approved, rejected hoặc needs_fix."),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Ghi chú của người duyệt."),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Người duyệt gần nhất. Xóa tài khoản thì để trống."),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Thời điểm duyệt gần nhất."),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Người tạo hoặc import. Trống với câu lấy từ chat."),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Xóa mềm: khác null thì chỉ hiện trong mục Đã xóa."),
                    deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Người xóa."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_base_entries", x => x.id);
                    table.CheckConstraint("ck_knowledge_base_entries_label", "label IS NULL OR label ~ '^LABEL_[A-Z0-9_]+$'");
                    table.CheckConstraint("ck_knowledge_base_entries_levels", "levels <@ ARRAY['chung','thcs','thpt','dai_hoc','thac_si','tien_si']::text[]");
                    table.CheckConstraint("ck_knowledge_base_entries_origin", "origin IN ('manual','excel','chat-unanswered')");
                    table.CheckConstraint("ck_knowledge_base_entries_ready", "review_status NOT IN ('pending','approved') OR (coalesce(btrim(answer),'') <> '' AND label IS NOT NULL AND coalesce(btrim(source),'') <> '' AND cardinality(levels) > 0)");
                    table.CheckConstraint("ck_knowledge_base_entries_review_status", "review_status IN ('draft','pending','approved','rejected','needs_fix')");
                    table.ForeignKey(
                        name: "fk_knowledge_base_entries_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_knowledge_base_entries_users_deleted_by_user_id",
                        column: x => x.deleted_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_knowledge_base_entries_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Kho tri thức do Moderator quản lý: câu hỏi, câu trả lời đã kiểm chứng, nhãn, bậc học, nguồn. Chỉ dòng approved chưa xóa được chat dùng.");

            migrationBuilder.CreateTable(
                name: "knowledge_base_entry_history",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    changed_fields = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_base_entry_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_base_entry_history_knowledge_base_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "app",
                        principalTable: "knowledge_base_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_knowledge_base_entry_history_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Lịch sử sửa và duyệt của mục kho tri thức.");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_entries_created_at",
                schema: "app",
                table: "knowledge_base_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_entries_created_by_user_id",
                schema: "app",
                table: "knowledge_base_entries",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_entries_deleted_by_user_id",
                schema: "app",
                table: "knowledge_base_entries",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_entries_entry_no",
                schema: "app",
                table: "knowledge_base_entries",
                column: "entry_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_entries_review_status_label",
                schema: "app",
                table: "knowledge_base_entries",
                columns: new[] { "review_status", "label" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_entries_reviewed_by_user_id",
                schema: "app",
                table: "knowledge_base_entries",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_entry_history_actor_user_id",
                schema: "app",
                table: "knowledge_base_entry_history",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_base_entry_history_entry_id_created_at",
                schema: "app",
                table: "knowledge_base_entry_history",
                columns: new[] { "entry_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_base_entry_history",
                schema: "app");

            migrationBuilder.DropTable(
                name: "knowledge_base_entries",
                schema: "app");

        }
    }
}
