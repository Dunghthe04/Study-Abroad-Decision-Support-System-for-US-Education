using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "avg_gpa4",
                schema: "app",
                table: "university_offerings",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sat25",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sat75",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_university_offerings_avg_gpa4",
                schema: "app",
                table: "university_offerings",
                sql: "avg_gpa4 IS NULL OR avg_gpa4 BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_university_offerings_sat_range",
                schema: "app",
                table: "university_offerings",
                sql: "(sat25 IS NULL OR sat25 BETWEEN 400 AND 1600) AND (sat75 IS NULL OR sat75 BETWEEN 400 AND 1600) AND (sat25 IS NULL OR sat75 IS NULL OR sat25 <= sat75)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_university_offerings_avg_gpa4",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_university_offerings_sat_range",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "avg_gpa4",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "sat25",
                schema: "app",
                table: "university_offerings");

            migrationBuilder.DropColumn(
                name: "sat75",
                schema: "app",
                table: "university_offerings");
        }
    }
}
