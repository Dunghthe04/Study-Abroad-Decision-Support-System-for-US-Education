using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCenterReviewReportsAndRepliesAndLeadReportDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "report_detail",
                schema: "app",
                table: "lead_messages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "center_reply_author_user_id",
                schema: "app",
                table: "center_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "center_reply_content",
                schema: "app",
                table: "center_reviews",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "center_reply_created_at",
                schema: "app",
                table: "center_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "center_reply_edited_at",
                schema: "app",
                table: "center_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "center_reply_remove_reason",
                schema: "app",
                table: "center_reviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "center_reply_removed_by_user_id",
                schema: "app",
                table: "center_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "center_reply_status",
                schema: "app",
                table: "center_reviews",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "center_review_reports",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    center_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_center_self_report = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "open"),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_center_review_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_center_review_reports_center_reviews_center_review_id",
                        column: x => x.center_review_id,
                        principalSchema: "app",
                        principalTable: "center_reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_center_review_reports_study_centers_study_center_id",
                        column: x => x.study_center_id,
                        principalSchema: "app",
                        principalTable: "study_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_center_review_reports_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_center_review_reports_users_resolved_by_user_id",
                        column: x => x.resolved_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_center_reviews_center_reply_author_user_id",
                schema: "app",
                table: "center_reviews",
                column: "center_reply_author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_reviews_center_reply_removed_by_user_id",
                schema: "app",
                table: "center_reviews",
                column: "center_reply_removed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_review_reports_center_review_id",
                schema: "app",
                table: "center_review_reports",
                column: "center_review_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_review_reports_reporter_user_id",
                schema: "app",
                table: "center_review_reports",
                column: "reporter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_review_reports_resolved_by_user_id",
                schema: "app",
                table: "center_review_reports",
                column: "resolved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_review_reports_status_created_at",
                schema: "app",
                table: "center_review_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_center_review_reports_study_center_id",
                schema: "app",
                table: "center_review_reports",
                column: "study_center_id");

            migrationBuilder.CreateIndex(
                name: "ux_center_review_reports_one_open_per_user",
                schema: "app",
                table: "center_review_reports",
                columns: new[] { "center_review_id", "reporter_user_id" },
                unique: true,
                filter: "status = 'open'");

            migrationBuilder.AddForeignKey(
                name: "fk_center_reviews_users_center_reply_author_user_id",
                schema: "app",
                table: "center_reviews",
                column: "center_reply_author_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_center_reviews_users_center_reply_removed_by_user_id",
                schema: "app",
                table: "center_reviews",
                column: "center_reply_removed_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_center_reviews_users_center_reply_author_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropForeignKey(
                name: "fk_center_reviews_users_center_reply_removed_by_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropTable(
                name: "center_review_reports",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_center_reviews_center_reply_author_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropIndex(
                name: "ix_center_reviews_center_reply_removed_by_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropColumn(
                name: "report_detail",
                schema: "app",
                table: "lead_messages");

            migrationBuilder.DropColumn(
                name: "center_reply_author_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropColumn(
                name: "center_reply_content",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropColumn(
                name: "center_reply_created_at",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropColumn(
                name: "center_reply_edited_at",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropColumn(
                name: "center_reply_remove_reason",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropColumn(
                name: "center_reply_removed_by_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropColumn(
                name: "center_reply_status",
                schema: "app",
                table: "center_reviews");
        }
    }
}
