using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStandardizedTestPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "standardized_planned_date",
                schema: "app",
                table: "student_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "standardized_planned_target_score",
                schema: "app",
                table: "student_profiles",
                type: "numeric(7,2)",
                precision: 7,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "standardized_planned_test_type",
                schema: "app",
                table: "student_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "standardized_test_planned",
                schema: "app",
                table: "student_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "standardized_planned_date",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "standardized_planned_target_score",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "standardized_planned_test_type",
                schema: "app",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "standardized_test_planned",
                schema: "app",
                table: "student_profiles");
        }
    }
}
