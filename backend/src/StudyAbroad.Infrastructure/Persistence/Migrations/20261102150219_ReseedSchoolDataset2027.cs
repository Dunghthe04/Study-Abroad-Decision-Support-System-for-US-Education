using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReseedSchoolDataset2027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_verified",
                schema: "app",
                table: "university_offering_majors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ranking_lists",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    publisher = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    list_group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    rank_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    edition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    edition_year = table.Column<int>(type: "integer", nullable: true),
                    url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ranking_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "university_rankings",
                schema: "app",
                columns: table => new
                {
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    list_id = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    is_tie = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    rank_display = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_university_rankings", x => new { x.university_id, x.list_id });
                    table.ForeignKey(
                        name: "fk_university_rankings_ranking_lists_list_id",
                        column: x => x.list_id,
                        principalSchema: "app",
                        principalTable: "ranking_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_university_rankings_universities_university_id",
                        column: x => x.university_id,
                        principalSchema: "app",
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_university_rankings_list_id",
                schema: "app",
                table: "university_rankings",
                column: "list_id");

            migrationBuilder.Sql("DELETE FROM app.universities;");
            migrationBuilder.Sql("DELETE FROM app.majors;");
            migrationBuilder.Sql(ReadSqlFile("20261102150219_ReseedSchoolDataset2027.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "university_rankings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ranking_lists",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "is_verified",
                schema: "app",
                table: "university_offering_majors");
        }

        private static string ReadSqlFile(string fileName)
        {
            var assembly = typeof(ReseedSchoolDataset2027).Assembly;
            var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith("." + fileName));
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
