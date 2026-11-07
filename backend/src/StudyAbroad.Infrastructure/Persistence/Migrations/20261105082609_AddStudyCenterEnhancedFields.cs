using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyCenterEnhancedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'app' AND table_name = 'study_centers' AND column_name = 'accreditations') THEN
                        UPDATE app.study_centers SET accreditations = '{}'::text[] WHERE accreditations IS NULL;
                    END IF;
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'app' AND table_name = 'study_centers' AND column_name = 'gallery_images') THEN
                        UPDATE app.study_centers SET gallery_images = '{}'::text[] WHERE gallery_images IS NULL;
                    END IF;
                END $$;
            """);
            migrationBuilder.AddColumn<List<string>>(
                name: "accreditations",
                schema: "app",
                table: "study_centers",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<decimal>(
                name: "average_rating",
                schema: "app",
                table: "study_centers",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 5.0m);

            migrationBuilder.AddColumn<string>(
                name: "center_type",
                schema: "app",
                table: "study_centers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "study_abroad");

            migrationBuilder.AddColumn<string>(
                name: "cover_image_url",
                schema: "app",
                table: "study_centers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "district",
                schema: "app",
                table: "study_centers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "established_year",
                schema: "app",
                table: "study_centers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "gallery_images",
                schema: "app",
                table: "study_centers",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "app",
                table: "study_centers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "license_number",
                schema: "app",
                table: "study_centers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                schema: "app",
                table: "study_centers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "app",
                table: "study_centers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "review_count",
                schema: "app",
                table: "study_centers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "staff_count",
                schema: "app",
                table: "study_centers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "visa_success_rate",
                schema: "app",
                table: "study_centers",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accreditations",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "average_rating",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "center_type",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "cover_image_url",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "district",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "established_year",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "gallery_images",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "license_number",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "logo_url",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "review_count",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "staff_count",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "visa_success_rate",
                schema: "app",
                table: "study_centers");
        }
    }
}
