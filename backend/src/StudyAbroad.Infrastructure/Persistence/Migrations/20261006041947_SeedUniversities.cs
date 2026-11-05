using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedUniversities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReadSqlFile("20261006041947_SeedUniversities.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReadSqlFile("20261006041947_SeedUniversities.down.sql"));
        }

        private static string ReadSqlFile(string fileName)
        {
            var assembly = typeof(SeedUniversities).Assembly;
            var resourceName = assembly.GetManifestResourceNames().Single(name => name.EndsWith("." + fileName));
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
