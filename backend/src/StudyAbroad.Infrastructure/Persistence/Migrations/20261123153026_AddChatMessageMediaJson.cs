using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageMediaJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "media_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                comment: "Ảnh minh họa của câu trả lời: [{kind, url, alt}]. Link https tới ảnh, lấy từ cột image_url của dòng kho tri thức được dùng; trình duyệt tải ảnh thẳng từ link. Null nếu không có ảnh.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "media_json",
                schema: "app",
                table: "chat_messages");
        }
    }
}
