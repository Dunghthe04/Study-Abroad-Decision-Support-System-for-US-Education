using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionColumnComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "chat_sessions",
                schema: "app",
                comment: "Một cuộc chat của một học sinh. Các tin nhắn nằm ở bảng chat_messages (nối bằng chat_session_id).");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "chat_sessions",
                type: "uuid",
                nullable: false,
                comment: "Chủ cuộc chat. Xóa tài khoản thì xóa luôn cuộc chat và các tin nhắn.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "study_level",
                schema: "app",
                table: "chat_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                comment: "Bậc học học sinh đang hỏi về.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "state_json",
                schema: "app",
                table: "chat_sessions",
                type: "jsonb",
                nullable: true,
                comment: "Trạng thái hội thoại advisor trả về sau mỗi lượt, gửi lại ở lượt sau. Nội bộ, không trả cho trình duyệt.",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "current_step",
                schema: "app",
                table: "chat_sessions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true,
                comment: "Bước hiện tại của hội thoại (B0..B6), cập nhật sau mỗi lượt.",
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "chat_session_id",
                schema: "app",
                table: "chat_messages",
                type: "uuid",
                nullable: false,
                comment: "Cuộc chat chứa tin này (bảng chat_sessions). Sắp theo created_at ra đúng thứ tự hội thoại.",
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "chat_sessions",
                schema: "app",
                oldComment: "Một cuộc chat của một học sinh. Các tin nhắn nằm ở bảng chat_messages (nối bằng chat_session_id).");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "chat_sessions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Chủ cuộc chat. Xóa tài khoản thì xóa luôn cuộc chat và các tin nhắn.");

            migrationBuilder.AlterColumn<string>(
                name: "study_level",
                schema: "app",
                table: "chat_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true,
                oldComment: "Bậc học học sinh đang hỏi về.");

            migrationBuilder.AlterColumn<string>(
                name: "state_json",
                schema: "app",
                table: "chat_sessions",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "Trạng thái hội thoại advisor trả về sau mỗi lượt, gửi lại ở lượt sau. Nội bộ, không trả cho trình duyệt.");

            migrationBuilder.AlterColumn<string>(
                name: "current_step",
                schema: "app",
                table: "chat_sessions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldNullable: true,
                oldComment: "Bước hiện tại của hội thoại (B0..B6), cập nhật sau mỗi lượt.");

            migrationBuilder.AlterColumn<Guid>(
                name: "chat_session_id",
                schema: "app",
                table: "chat_messages",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Cuộc chat chứa tin này (bảng chat_sessions). Sắp theo created_at ra đúng thứ tự hội thoại.");
        }
    }
}
