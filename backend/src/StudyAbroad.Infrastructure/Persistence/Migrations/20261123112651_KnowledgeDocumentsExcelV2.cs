using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeDocumentsExcelV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM app.knowledge_documents;");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_documents_users_verified_by_user_id",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropTable(
                name: "knowledge_base_entry_history",
                schema: "app");

            migrationBuilder.DropTable(
                name: "slm_sample_history",
                schema: "app");

            migrationBuilder.DropTable(
                name: "knowledge_base_entries",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_code",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_scope",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_status_study_level_doc_type",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_topic",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_verified_by_user_id",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "review_due_at",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "scope",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "source_note",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "source_type",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "study_level",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "topic",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "verified_at",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "verified_by_user_id",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.RenameColumn(
                name: "quote",
                schema: "app",
                table: "knowledge_documents",
                newName: "source");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "app",
                table: "knowledge_documents",
                newName: "label");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                schema: "app",
                table: "slm_training_samples",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "manual",
                comment: "Nguồn: manual, excel hoặc nguồn tham khảo.",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldDefaultValue: "manual",
                oldComment: "Nguồn: manual, excel, chat-unanswered hoặc nguồn tham khảo.");

            migrationBuilder.AlterColumn<string>(
                name: "source_url",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "retrieved_at",
                schema: "app",
                table: "knowledge_documents",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "question",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "doc_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "faq");

            migrationBuilder.AddColumn<int>(
                name: "entry_no",
                schema: "app",
                table: "knowledge_documents",
                type: "integer",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "topic_code",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_doc_type",
                schema: "app",
                table: "knowledge_documents",
                column: "doc_type");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_entry_no",
                schema: "app",
                table: "knowledge_documents",
                column: "entry_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_label",
                schema: "app",
                table: "knowledge_documents",
                column: "label");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_topic_code",
                schema: "app",
                table: "knowledge_documents",
                column: "topic_code");

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_documents_doc_type",
                schema: "app",
                table: "knowledge_documents",
                sql: "doc_type IN ('faq','glossary','proc_overview','proc_prepare','proc_steps','proc_checklist','proc_mistakes','timeline','compare','situation','data_table','resource','notice')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM app.knowledge_documents;");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_doc_type",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_entry_no",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_label",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_topic_code",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_documents_doc_type",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "entry_no",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "image_url",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "note",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "topic_code",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.RenameColumn(
                name: "source",
                schema: "app",
                table: "knowledge_documents",
                newName: "quote");

            migrationBuilder.RenameColumn(
                name: "label",
                schema: "app",
                table: "knowledge_documents",
                newName: "code");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                schema: "app",
                table: "slm_training_samples",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "manual",
                comment: "Nguồn: manual, excel, chat-unanswered hoặc nguồn tham khảo.",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldDefaultValue: "manual",
                oldComment: "Nguồn: manual, excel hoặc nguồn tham khảo.");

            migrationBuilder.AlterColumn<string>(
                name: "source_url",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "retrieved_at",
                schema: "app",
                table: "knowledge_documents",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "question",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "doc_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "faq",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<DateTime>(
                name: "review_due_at",
                schema: "app",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scope",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "system_advisory");

            migrationBuilder.AddColumn<string>(
                name: "source_note",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "official");

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft");

            migrationBuilder.AddColumn<string>(
                name: "study_level",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "general");

            migrationBuilder.AddColumn<string>(
                name: "topic",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "verified_at",
                schema: "app",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by_user_id",
                schema: "app",
                table: "knowledge_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "app",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "knowledge_base_entries",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true, comment: "Câu trả lời đã kiểm chứng; nháp có thể trống."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Người tạo hoặc import. Trống với câu lấy từ chat."),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Xóa mềm: khác null thì chỉ hiện trong mục Đã xóa."),
                    deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Người xóa."),
                    entry_no = table.Column<int>(type: "integer", nullable: false, comment: "Mã số ổn định, trùng cột id của file Excel. Thêm tay thì database cấp số tiếp theo.")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true, comment: "Nhãn chủ đề LABEL_...; nháp có thể trống."),
                    levels = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'", comment: "Mã bậc học: chung, thcs, thpt, dai_hoc, thac_si, tien_si."),
                    origin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "manual", comment: "Dòng đến từ đâu: manual, excel hoặc chat-unanswered."),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "Câu hỏi."),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Ghi chú của người duyệt."),
                    review_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft", comment: "Trạng thái duyệt: draft, pending, approved, rejected hoặc needs_fix."),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Thời điểm duyệt gần nhất."),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Người duyệt gần nhất. Xóa tài khoản thì để trống."),
                    source = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "Trích dẫn nguồn, giữ nguyên văn nhiều dòng."),
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
                name: "slm_sample_history",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_fields = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sample_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_slm_sample_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_slm_sample_history_slm_training_samples_sample_id",
                        column: x => x.sample_id,
                        principalSchema: "app",
                        principalTable: "slm_training_samples",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_slm_sample_history_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Lịch sử sửa và duyệt của mẫu SLM.");

            migrationBuilder.CreateTable(
                name: "knowledge_base_entry_history",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_fields = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
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
                name: "ix_knowledge_documents_code",
                schema: "app",
                table: "knowledge_documents",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_scope",
                schema: "app",
                table: "knowledge_documents",
                column: "scope");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_status_study_level_doc_type",
                schema: "app",
                table: "knowledge_documents",
                columns: new[] { "status", "study_level", "doc_type" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_topic",
                schema: "app",
                table: "knowledge_documents",
                column: "topic");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_verified_by_user_id",
                schema: "app",
                table: "knowledge_documents",
                column: "verified_by_user_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_slm_sample_history_actor_user_id",
                schema: "app",
                table: "slm_sample_history",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_slm_sample_history_sample_id_created_at",
                schema: "app",
                table: "slm_sample_history",
                columns: new[] { "sample_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_documents_users_verified_by_user_id",
                schema: "app",
                table: "knowledge_documents",
                column: "verified_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
