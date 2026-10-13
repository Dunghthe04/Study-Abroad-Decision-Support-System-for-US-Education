using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationToProfileActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS: DB đã chạy bản cũ của 20261008052916 (cũng thêm 2 cột này) thì bỏ qua, không lỗi "column already exists"
            migrationBuilder.Sql("ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS duration_months integer;");
            migrationBuilder.Sql("ALTER TABLE app.profile_activities ADD COLUMN IF NOT EXISTS is_ongoing boolean NOT NULL DEFAULT false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_months",
                schema: "app",
                table: "profile_activities");

            migrationBuilder.DropColumn(
                name: "is_ongoing",
                schema: "app",
                table: "profile_activities");
        }
    }
}
