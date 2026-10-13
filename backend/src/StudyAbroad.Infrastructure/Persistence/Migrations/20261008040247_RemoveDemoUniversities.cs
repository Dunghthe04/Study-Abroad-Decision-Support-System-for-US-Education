using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Xóa 9 trường demo (DEMO_UA … DEMO_UI) mà DbSeeder cũ (commit f9428d1) đã thêm vào DB local.
    /// Seeder đã bỏ phần demo nhưng dữ liệu còn trong DB, làm "Demo University" lẫn vào kết quả gợi ý trường.
    /// </summary>
    public partial class RemoveDemoUniversities : Migration
    {
        private const string DemoCodes = "'DEMO_UA','DEMO_UB','DEMO_UC','DEMO_UD','DEMO_UE','DEMO_UF','DEMO_UG','DEMO_UH','DEMO_UI'";
        private const string DemoIds = $"SELECT id FROM app.universities WHERE code IN ({DemoCodes})";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DELETE FROM app.target_schools WHERE university_id IN ({DemoIds});");
            migrationBuilder.Sql($"DELETE FROM app.scholarships WHERE university_id IN ({DemoIds});");
            migrationBuilder.Sql($"DELETE FROM app.university_offerings WHERE university_id IN ({DemoIds});");
            migrationBuilder.Sql($"DELETE FROM app.universities WHERE code IN ({DemoCodes});");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dữ liệu demo không khôi phục lại
        }
    }
}
