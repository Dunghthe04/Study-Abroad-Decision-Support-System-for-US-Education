using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDateOfBirthAndParentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                schema: "app",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "granted_by_user_id",
                schema: "app",
                table: "consents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "granted_ip",
                schema: "app",
                table: "consents",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "parent_student_links",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    parent_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    invite_sent_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_invite_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    // Không tạo cột xmin: Version ánh xạ vào cột hệ thống xmin có sẵn của Postgres.
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parent_student_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_parent_student_links_consents_consent_id",
                        column: x => x.consent_id,
                        principalSchema: "app",
                        principalTable: "consents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_parent_student_links_users_parent_user_id",
                        column: x => x.parent_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_parent_student_links_users_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_parent_student_links_users_student_user_id",
                        column: x => x.student_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consents_granted_by_user_id",
                schema: "app",
                table: "consents",
                column: "granted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_parent_student_links_consent_id",
                schema: "app",
                table: "parent_student_links",
                column: "consent_id");

            migrationBuilder.CreateIndex(
                name: "ix_parent_student_links_parent_user_id_status",
                schema: "app",
                table: "parent_student_links",
                columns: new[] { "parent_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_parent_student_links_revoked_by_user_id",
                schema: "app",
                table: "parent_student_links",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_parent_student_links_student_user_id_parent_email",
                schema: "app",
                table: "parent_student_links",
                columns: new[] { "student_user_id", "parent_email" },
                unique: true,
                filter: "status IN ('pending', 'active')");

            migrationBuilder.CreateIndex(
                name: "ix_parent_student_links_token_hash",
                schema: "app",
                table: "parent_student_links",
                column: "token_hash",
                unique: true,
                filter: "token_hash IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_consents_users_granted_by_user_id",
                schema: "app",
                table: "consents",
                column: "granted_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_consents_users_granted_by_user_id",
                schema: "app",
                table: "consents");

            migrationBuilder.DropTable(
                name: "parent_student_links",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_consents_granted_by_user_id",
                schema: "app",
                table: "consents");

            migrationBuilder.DropColumn(
                name: "date_of_birth",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "granted_by_user_id",
                schema: "app",
                table: "consents");

            migrationBuilder.DropColumn(
                name: "granted_ip",
                schema: "app",
                table: "consents");
        }
    }
}
