using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "us_states",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    region = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_popular = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_us_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_us_states_code",
                schema: "app",
                table: "us_states",
                column: "code",
                unique: true);

            migrationBuilder.Sql(ReadSqlFile("20261102183343_AddUsStates.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "us_states",
                schema: "app");
        }

        private static string ReadSqlFile(string fileName)
        {
            var assembly = typeof(AddUsStates).Assembly;
            var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith("." + fileName));
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
