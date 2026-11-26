using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthDateChangeRequestsAndLeadHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "held_at",
                schema: "app",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "held_from_status",
                schema: "app",
                table: "leads",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "paused_seconds",
                schema: "app",
                table: "leads",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "birth_date_change_requests",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    requested_birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    evidence_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    evidence_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    evidence_file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    evidence_content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    evidence_size = table.Column<long>(type: "bigint", nullable: true),
                    representative_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_birth_date_change_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_birth_date_change_requests_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_birth_date_change_requests_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_birth_date_change_requests_reviewed_by_user_id",
                schema: "app",
                table: "birth_date_change_requests",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_birth_date_change_requests_status_created_at",
                schema: "app",
                table: "birth_date_change_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_birth_date_change_requests_user_id",
                schema: "app",
                table: "birth_date_change_requests",
                column: "user_id",
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_birth_date_change_requests_user_id_created_at",
                schema: "app",
                table: "birth_date_change_requests",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "birth_date_change_requests",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "held_at",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "held_from_status",
                schema: "app",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "paused_seconds",
                schema: "app",
                table: "leads");
        }
    }
}
