using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // duration_months, is_ongoing đã thêm ở 20261008043239_AddDurationToProfileActivities: migration này chỉ thêm 2 cột tài chính
    public partial class AddFinancialDetailsAndActivityDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "financial_notes",
                schema: "app",
                table: "student_profiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "max_expected_tuition_usd",
                schema: "app",
                table: "student_profiles",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "financial_notes",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "max_expected_tuition_usd",
                schema: "app",
                table: "student_profiles");
        }
    }
}
