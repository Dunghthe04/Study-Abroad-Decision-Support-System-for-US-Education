using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCenterProfileChangesAndDeletionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fanpage_url",
                schema: "app",
                table: "study_centers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "center_deletion_requests",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_center_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    center_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "pending"),
                    due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    identity_verified = table.Column<bool>(type: "boolean", nullable: false),
                    processed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    admin_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_center_deletion_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_center_deletion_requests_study_centers_study_center_id",
                        column: x => x.study_center_id,
                        principalSchema: "app",
                        principalTable: "study_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_center_deletion_requests_users_processed_by_user_id",
                        column: x => x.processed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_center_deletion_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "center_profile_changes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    old_value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    new_value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "pending"),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_center_profile_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_center_profile_changes_study_centers_study_center_id",
                        column: x => x.study_center_id,
                        principalSchema: "app",
                        principalTable: "study_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_center_profile_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_center_profile_changes_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_center_deletion_requests_processed_by_user_id",
                schema: "app",
                table: "center_deletion_requests",
                column: "processed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_deletion_requests_requested_by_user_id",
                schema: "app",
                table: "center_deletion_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_deletion_requests_status_created_at",
                schema: "app",
                table: "center_deletion_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_center_deletion_requests_study_center_id",
                schema: "app",
                table: "center_deletion_requests",
                column: "study_center_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_profile_changes_changed_by_user_id",
                schema: "app",
                table: "center_profile_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_profile_changes_reviewed_by_user_id",
                schema: "app",
                table: "center_profile_changes",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_center_profile_changes_status_created_at",
                schema: "app",
                table: "center_profile_changes",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_center_profile_changes_study_center_id_field",
                schema: "app",
                table: "center_profile_changes",
                columns: new[] { "study_center_id", "field" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "center_deletion_requests",
                schema: "app");

            migrationBuilder.DropTable(
                name: "center_profile_changes",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "fanpage_url",
                schema: "app",
                table: "study_centers");
        }
    }
}
