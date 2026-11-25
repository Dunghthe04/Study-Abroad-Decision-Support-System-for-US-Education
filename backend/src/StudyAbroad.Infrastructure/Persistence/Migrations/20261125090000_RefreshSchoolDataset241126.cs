using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyAbroad.Infrastructure.Persistence;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261125090000_RefreshSchoolDataset241126")]
    public partial class RefreshSchoolDataset241126 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var assembly = typeof(RefreshSchoolDataset241126).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith(".20261125090000_RefreshSchoolDataset241126.sql", StringComparison.Ordinal));
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
