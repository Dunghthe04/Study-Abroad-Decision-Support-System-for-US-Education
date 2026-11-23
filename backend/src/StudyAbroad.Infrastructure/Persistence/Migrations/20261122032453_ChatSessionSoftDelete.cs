using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChatSessionSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                schema: "app",
                table: "chat_sessions",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Học sinh xóa cuộc trò chuyện khỏi lịch sử (xóa mềm): không hiện, không mở lại, không hỏi tiếp. Tin nhắn vẫn giữ cho thống kê. Null là chưa xóa.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "app",
                table: "chat_sessions");
        }
    }
}
