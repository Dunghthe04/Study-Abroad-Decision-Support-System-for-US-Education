using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlmSampleLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_slm_training_samples_review_status",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.AlterColumn<string>(
                name: "review_status",
                schema: "app",
                table: "slm_training_samples",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                comment: "Trạng thái duyệt: draft, pending, approved, rejected hoặc needs_fix.",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft",
                oldComment: "Trạng thái duyệt: draft, approved, rejected hoặc needs_fix.");

            migrationBuilder.AddColumn<string>(
                name: "answer",
                schema: "app",
                table: "slm_training_samples",
                type: "text",
                nullable: true,
                comment: "Câu trả lời mẫu. Dòng cũ chỉ có intents thì để trống.");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                schema: "app",
                table: "slm_training_samples",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Xóa mềm: khác null thì không hiện, không xuất.");

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                schema: "app",
                table: "slm_training_samples",
                type: "uuid",
                nullable: true,
                comment: "Người xóa.");

            migrationBuilder.AddColumn<DateTime>(
                name: "exported_at",
                schema: "app",
                table: "slm_training_samples",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Lần xuất gần nhất.");

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "app",
                table: "slm_training_samples",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "manual",
                comment: "Nguồn: manual, excel, chat-unanswered hoặc nguồn tham khảo.");

            migrationBuilder.AddColumn<string>(
                name: "topic",
                schema: "app",
                table: "slm_training_samples",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                comment: "Chủ đề.");

            migrationBuilder.CreateTable(
                name: "slm_sample_history",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sample_id = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "ix_slm_training_samples_deleted_by_user_id",
                schema: "app",
                table: "slm_training_samples",
                column: "deleted_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_slm_training_samples_review_status",
                schema: "app",
                table: "slm_training_samples",
                sql: "review_status IN ('draft','pending','approved','rejected','needs_fix')");

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
                name: "fk_slm_training_samples_users_deleted_by_user_id",
                schema: "app",
                table: "slm_training_samples",
                column: "deleted_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_slm_training_samples_users_deleted_by_user_id",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropTable(
                name: "slm_sample_history",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_slm_training_samples_deleted_by_user_id",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropCheckConstraint(
                name: "ck_slm_training_samples_review_status",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "answer",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "exported_at",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.DropColumn(
                name: "topic",
                schema: "app",
                table: "slm_training_samples");

            migrationBuilder.AlterColumn<string>(
                name: "review_status",
                schema: "app",
                table: "slm_training_samples",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                comment: "Trạng thái duyệt: draft, approved, rejected hoặc needs_fix.",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft",
                oldComment: "Trạng thái duyệt: draft, pending, approved, rejected hoặc needs_fix.");

            migrationBuilder.AddCheckConstraint(
                name: "ck_slm_training_samples_review_status",
                schema: "app",
                table: "slm_training_samples",
                sql: "review_status IN ('draft','approved','rejected','needs_fix')");
        }
    }
}
