using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCostEstimateData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "act_policy",
                schema: "app",
                table: "university_offerings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "act_submit_pct",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "application_fee_usd",
                schema: "app",
                table: "university_offerings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "health_insurance_usd",
                schema: "app",
                table: "university_offerings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tuition_in_state_usd",
                schema: "app",
                table: "university_offerings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "amount_max_usd",
                schema: "app",
                table: "scholarships",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "amount_min_usd",
                schema: "app",
                table: "scholarships",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "amount_type",
                schema: "app",
                table: "scholarships",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "application_method",
                schema: "app",
                table: "scholarships",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "apply_url",
                schema: "app",
                table: "scholarships",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration_years",
                schema: "app",
                table: "scholarships",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "intl_eligible",
                schema: "app",
                table: "scholarships",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "app",
                table: "scholarships",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "major_restriction",
                schema: "app",
                table: "scholarships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "merit_based",
                schema: "app",
                table: "scholarships",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "min_act",
                schema: "app",
                table: "scholarships",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "min_sat",
                schema: "app",
                table: "scholarships",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "min_toefl",
                schema: "app",
                table: "scholarships",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "need_based",
                schema: "app",
                table: "scholarships",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "renewable",
                schema: "app",
                table: "scholarships",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "renewal_criteria",
                schema: "app",
                table: "scholarships",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "vn_eligible",
                schema: "app",
                table: "scholarships",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_us_states_code",
                schema: "app",
                table: "us_states",
                column: "code");

            migrationBuilder.CreateTable(
                name: "state_living_costs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false),
                    index_all_items = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    index_housing_rents = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    index_goods = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    index_other_services = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    source_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    retrieved_at = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_state_living_costs", x => x.id);
                    table.ForeignKey(
                        name: "fk_state_living_costs_us_states_state_code",
                        column: x => x.state_code,
                        principalSchema: "app",
                        principalTable: "us_states",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "[USAS-2067] Chỉ số giá khu vực theo bang (BEA RPP, Mỹ = 100), mỗi bang mỗi năm một dòng. Không phải USD.");

            migrationBuilder.CreateIndex(
                name: "ix_state_living_costs_state_code_year",
                schema: "app",
                table: "state_living_costs",
                columns: new[] { "state_code", "year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "state_living_costs",
                schema: "app");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_us_states_code",
                schema: "app",
                table: "us_states");

            migrationBuilder.DropColumn(
                name: "act_policy",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "act_submit_pct",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "application_fee_usd",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "health_insurance_usd",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "tuition_in_state_usd",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "amount_max_usd",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "amount_min_usd",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "amount_type",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "application_method",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "apply_url",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "duration_years",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "intl_eligible",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "major_restriction",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "merit_based",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "min_act",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "min_sat",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "min_toefl",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "need_based",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "renewable",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "renewal_criteria",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "vn_eligible",
                schema: "app",
                table: "scholarships");
        }
    }
}
