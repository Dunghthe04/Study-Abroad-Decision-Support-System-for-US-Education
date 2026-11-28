using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedReservedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expert_rules",
                schema: "app");

            migrationBuilder.DropTable(
                name: "policy_alerts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "roadmap_steps",
                schema: "app");

            migrationBuilder.DropTable(
                name: "user_daily_usages",
                schema: "app");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expert_rules",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_recommendation = table.Column<string>(type: "text", nullable: true),
                    certainty_factor = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0.90m),
                    condition_json = table.Column<string>(type: "jsonb", nullable: true),
                    condition_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    degree_level = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "all"),
                    rule_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rule_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "active"),
                    supporting_centers_count = table.Column<int>(type: "integer", nullable: false),
                    target_direction = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "USAS Advisory Board"),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expert_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "policy_alerts",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "visa"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "info"),
                    source_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roadmap_steps",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    months_before_start = table.Column<int>(type: "integer", nullable: true),
                    resources_json = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    step_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    study_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roadmap_steps", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_daily_usages",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ai_chat_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    leads_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usage_date = table.Column<DateOnly>(type: "date", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_daily_usages", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_daily_usages_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expert_rules_degree_level",
                schema: "app",
                table: "expert_rules",
                column: "degree_level");

            migrationBuilder.CreateIndex(
                name: "ix_expert_rules_rule_code",
                schema: "app",
                table: "expert_rules",
                column: "rule_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roadmap_steps_study_level_step_key",
                schema: "app",
                table: "roadmap_steps",
                columns: new[] { "study_level", "step_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_daily_usages_user_id_usage_date",
                schema: "app",
                table: "user_daily_usages",
                columns: new[] { "user_id", "usage_date" },
                unique: true);
        }
    }
}
