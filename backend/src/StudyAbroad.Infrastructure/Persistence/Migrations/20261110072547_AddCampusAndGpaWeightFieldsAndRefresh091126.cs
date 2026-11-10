using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampusAndGpaWeightFieldsAndRefresh091126 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "activities",
                schema: "app",
                table: "university_offerings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "avg_gpa_weighted",
                schema: "app",
                table: "university_offerings",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "campus_setting",
                schema: "app",
                table: "university_offerings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fy_on_campus_pct",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gpa_scale_type",
                schema: "app",
                table: "university_offerings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "housing_deposit_usd",
                schema: "app",
                table: "university_offerings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "housing_only_usd",
                schema: "app",
                table: "university_offerings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "housing_types",
                schema: "app",
                table: "university_offerings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "intl_housing",
                schema: "app",
                table: "university_offerings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "student_faculty_ratio",
                schema: "app",
                table: "university_offerings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ug_on_campus_pct",
                schema: "app",
                table: "university_offerings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_university_offerings_avg_gpa_weighted",
                schema: "app",
                table: "university_offerings",
                sql: "avg_gpa_weighted IS NULL OR avg_gpa_weighted >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_university_offerings_gpa_scale_type",
                schema: "app",
                table: "university_offerings",
                sql: "gpa_scale_type IS NULL OR gpa_scale_type IN ('weighted', 'unweighted', 'both')");

            var assembly = typeof(AddCampusAndGpaWeightFieldsAndRefresh091126).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith(".20261110072547_AddCampusAndGpaWeightFieldsAndRefresh091126.sql", StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            migrationBuilder.Sql(reader.ReadToEnd());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("This data refresh cannot restore overwritten values. Restore a database backup instead.");
        }
    }
}
