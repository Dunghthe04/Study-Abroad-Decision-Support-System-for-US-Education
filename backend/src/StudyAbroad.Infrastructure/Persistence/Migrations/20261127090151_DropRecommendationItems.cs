using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRecommendationItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recommendation_items",
                schema: "app");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recommendation_items",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    financial_fit_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "affordable"),
                    gap_reasons = table.Column<string>(type: "text", nullable: true),
                    is_tier_divergent = table.Column<bool>(type: "boolean", nullable: false),
                    match_strength = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    program_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_codes_triggered = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    tier_ai = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tier_rule = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_recommendation_items_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalSchema: "app",
                        principalTable: "recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recommendation_items_universities_university_id",
                        column: x => x.university_id,
                        principalSchema: "app",
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_items_recommendation_id",
                schema: "app",
                table: "recommendation_items",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_items_university_id",
                schema: "app",
                table: "recommendation_items",
                column: "university_id");
        }
    }
}
