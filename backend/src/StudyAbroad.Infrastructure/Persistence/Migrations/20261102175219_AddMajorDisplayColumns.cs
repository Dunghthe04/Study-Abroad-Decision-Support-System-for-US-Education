using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMajorDisplayColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "aliases",
                schema: "app",
                table: "majors",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<bool>(
                name: "is_popular",
                schema: "app",
                table: "majors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "name_vi",
                schema: "app",
                table: "majors",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql(ReadSqlFile("20261102175219_AddMajorDisplayColumns.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aliases",
                schema: "app",
                table: "majors");

            migrationBuilder.DropColumn(
                name: "is_popular",
                schema: "app",
                table: "majors");

            migrationBuilder.DropColumn(
                name: "name_vi",
                schema: "app",
                table: "majors");
        }

        private static string ReadSqlFile(string fileName)
        {
            var assembly = typeof(AddMajorDisplayColumns).Assembly;
            var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith("." + fileName));
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
