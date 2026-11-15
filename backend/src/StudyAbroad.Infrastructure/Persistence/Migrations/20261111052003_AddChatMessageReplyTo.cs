using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageReplyTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "meta_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                comment: "Tóm tắt lượt (JSON), mỗi thông tin ở tin nó mô tả. Tin của HỌC SINH: slm = nhãn model gán cho câu hỏi (modelIntents, intents, confidence, fallback). Tin TRỢ LÝ: local và cloud (số lần gọi, token, thời gian của AI local và AI cloud), turnMs, action, skills, failedSteps. Lọc được vào từng khóa, ví dụ meta_json -> 'slm' ->> 'fallback' = 'true'.",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "Tóm tắt lượt cho admin (JSON): intents (các ý SLM tách), actions, slmFallback, confidence, totalLatencyMs, totalTokens. Chỉ ở tin trả lời của trợ lý. Lọc được vào từng khóa, ví dụ meta_json ->> 'slmFallback' = 'true'.");

            migrationBuilder.AddColumn<Guid>(
                name: "reply_to_message_id",
                schema: "app",
                table: "chat_messages",
                type: "uuid",
                nullable: true,
                comment: "Tin của học sinh mà tin trợ lý này trả lời, nối chính xác một câu hỏi với một câu trả lời. Chỉ có ở tin trợ lý.");

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_reply_to_message_id",
                schema: "app",
                table: "chat_messages",
                column: "reply_to_message_id");

            migrationBuilder.AddForeignKey(
                name: "fk_chat_messages_chat_messages_reply_to_message_id",
                schema: "app",
                table: "chat_messages",
                column: "reply_to_message_id",
                principalSchema: "app",
                principalTable: "chat_messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_chat_messages_chat_messages_reply_to_message_id",
                schema: "app",
                table: "chat_messages");

            migrationBuilder.DropIndex(
                name: "ix_chat_messages_reply_to_message_id",
                schema: "app",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "reply_to_message_id",
                schema: "app",
                table: "chat_messages");

            migrationBuilder.AlterColumn<string>(
                name: "meta_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                comment: "Tóm tắt lượt cho admin (JSON): intents (các ý SLM tách), actions, slmFallback, confidence, totalLatencyMs, totalTokens. Chỉ ở tin trả lời của trợ lý. Lọc được vào từng khóa, ví dụ meta_json ->> 'slmFallback' = 'true'.",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "Tóm tắt lượt (JSON), mỗi thông tin ở tin nó mô tả. Tin của HỌC SINH: slm = nhãn model gán cho câu hỏi (modelIntents, intents, confidence, fallback). Tin TRỢ LÝ: local và cloud (số lần gọi, token, thời gian của AI local và AI cloud), turnMs, action, skills, failedSteps. Lọc được vào từng khóa, ví dụ meta_json -> 'slm' ->> 'fallback' = 'true'.");
        }
    }
}
