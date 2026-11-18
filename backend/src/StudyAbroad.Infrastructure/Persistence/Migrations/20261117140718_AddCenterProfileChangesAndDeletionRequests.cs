using System;
using System.Collections.Generic;
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
            migrationBuilder.AddColumn<List<string>>(
                name: "branches",
                schema: "app",
                table: "study_centers",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "fanpage",
                schema: "app",
                table: "study_centers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "service_areas",
                schema: "app",
                table: "study_centers",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "center_deletion_requests",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    handled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_center_deletion_requests_users_handled_by_user_id",
                        column: x => x.handled_by_user_id,
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "center_profile_changes",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    study_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    old_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    new_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                        name: "fk_center_profile_changes_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_center_deletion_requests_handled_by_user_id",
                schema: "app",
                table: "center_deletion_requests",
                column: "handled_by_user_id");

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
                name: "ix_center_profile_changes_study_center_id",
                schema: "app",
                table: "center_profile_changes",
                column: "study_center_id");
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
                name: "branches",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "fanpage",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "service_areas",
                schema: "app",
                table: "study_centers");
        }
    }
}
