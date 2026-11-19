using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>Refresh token: thêm hash refresh token hiện tại và hash vừa bị đổi vào user_sessions.
    /// Cột legal hold của users / study_centers đã có trong migration AddCenterDeletionHandling nên không thêm lại ở đây
    /// (snapshot trước đó thiếu chúng nên EF tự sinh, đã bỏ ra).</summary>
    public partial class AddRefreshTokenToUserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "previous_refresh_token_hash",
                schema: "app",
                table: "user_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refresh_token_hash",
                schema: "app",
                table: "user_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_previous_refresh_token_hash",
                schema: "app",
                table: "user_sessions",
                column: "previous_refresh_token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_refresh_token_hash",
                schema: "app",
                table: "user_sessions",
                column: "refresh_token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_sessions_previous_refresh_token_hash",
                schema: "app",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "ix_user_sessions_refresh_token_hash",
                schema: "app",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "previous_refresh_token_hash",
                schema: "app",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "refresh_token_hash",
                schema: "app",
                table: "user_sessions");
        }
    }
}
