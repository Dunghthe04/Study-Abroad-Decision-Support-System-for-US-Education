using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshSchoolDataset051126 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var assembly = typeof(RefreshSchoolDataset051126).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith(".20261105041027_RefreshSchoolDataset051126.sql", StringComparison.Ordinal));
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
