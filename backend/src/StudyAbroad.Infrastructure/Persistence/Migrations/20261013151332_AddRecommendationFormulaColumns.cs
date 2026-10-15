using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationFormulaColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "c7_ec_importance",
                schema: "app",
                table: "university_offerings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "c7_gpa_importance",
                schema: "app",
                table: "university_offerings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "c7_test_importance",
                schema: "app",
                table: "university_offerings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gpa_pct_250_299",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gpa_pct_300_324",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gpa_pct_325_349",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gpa_pct_350_374",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gpa_pct_375_399",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gpa_pct_4_0",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gpa_pct_below_250",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "grad_rate_6yr",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "intl_acceptance_rate",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "intl_aid_avg_usd",
                schema: "app",
                table: "university_offerings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "intl_aid_policy",
                schema: "app",
                table: "university_offerings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "retention_rate_1yr",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sat_submit_pct",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "c7_ec_importance",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "c7_gpa_importance",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "c7_test_importance",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "gpa_pct_250_299",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "gpa_pct_300_324",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "gpa_pct_325_349",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "gpa_pct_350_374",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "gpa_pct_375_399",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "gpa_pct_4_0",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "gpa_pct_below_250",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "grad_rate_6yr",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "intl_acceptance_rate",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "intl_aid_avg_usd",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "intl_aid_policy",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "retention_rate_1yr",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "sat_submit_pct",
                schema: "app",
                table: "university_offerings");
        }
    }
}
