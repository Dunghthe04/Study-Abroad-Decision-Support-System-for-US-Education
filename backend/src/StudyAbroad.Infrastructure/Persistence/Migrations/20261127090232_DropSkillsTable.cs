using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropSkillsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_chat_sessions_skills_skill_id",
                schema: "app",
                table: "chat_sessions");

            migrationBuilder.DropTable(
                name: "skills",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_chat_sessions_skill_id",
                schema: "app",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "skill_id",
                schema: "app",
                table: "eval_runs");

            migrationBuilder.DropColumn(
                name: "skill_id",
                schema: "app",
                table: "chat_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "skill_id",
                schema: "app",
                table: "eval_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "skill_id",
                schema: "app",
                table: "chat_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skills", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_skill_id",
                schema: "app",
                table: "chat_sessions",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_key_version",
                schema: "app",
                table: "skills",
                columns: new[] { "key", "version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_chat_sessions_skills_skill_id",
                schema: "app",
                table: "chat_sessions",
                column: "skill_id",
                principalSchema: "app",
                principalTable: "skills",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
