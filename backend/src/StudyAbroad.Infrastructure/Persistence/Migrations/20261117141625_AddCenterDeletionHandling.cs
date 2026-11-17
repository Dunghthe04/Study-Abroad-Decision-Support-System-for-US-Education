using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCenterDeletionHandling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_center_deletion_requests_users_requested_by_user_id",
                schema: "app",
                table: "center_deletion_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "requested_by_user_id",
                schema: "app",
                table: "center_deletion_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                schema: "app",
                table: "center_deletion_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_note",
                schema: "app",
                table: "center_deletion_requests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "scheduled_purge_at",
                schema: "app",
                table: "center_deletion_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_legal_hold",
                schema: "app",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "legal_hold_reason",
                schema: "app",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_legal_hold",
                schema: "app",
                table: "study_centers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "legal_hold_reason",
                schema: "app",
                table: "study_centers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_center_deletion_requests_status_scheduled_purge_at",
                schema: "app",
                table: "center_deletion_requests",
                columns: new[] { "status", "scheduled_purge_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_center_deletion_requests_users_requested_by_user_id",
                schema: "app",
                table: "center_deletion_requests",
                column: "requested_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_center_deletion_requests_users_requested_by_user_id",
                schema: "app",
                table: "center_deletion_requests");

            migrationBuilder.DropIndex(
                name: "ix_center_deletion_requests_status_scheduled_purge_at",
                schema: "app",
                table: "center_deletion_requests");

            migrationBuilder.DropColumn(
                name: "completed_at",
                schema: "app",
                table: "center_deletion_requests");

            migrationBuilder.DropColumn(
                name: "decision_note",
                schema: "app",
                table: "center_deletion_requests");

            migrationBuilder.DropColumn(
                name: "scheduled_purge_at",
                schema: "app",
                table: "center_deletion_requests");

            migrationBuilder.DropColumn(
                name: "is_legal_hold",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "legal_hold_reason",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_legal_hold",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "legal_hold_reason",
                schema: "app",
                table: "study_centers");

            migrationBuilder.AlterColumn<Guid>(
                name: "requested_by_user_id",
                schema: "app",
                table: "center_deletion_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_center_deletion_requests_users_requested_by_user_id",
                schema: "app",
                table: "center_deletion_requests",
                column: "requested_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
