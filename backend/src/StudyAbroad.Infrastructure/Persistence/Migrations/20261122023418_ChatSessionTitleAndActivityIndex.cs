using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChatSessionTitleAndActivityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "app",
                table: "chat_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "Tiêu đề trong lịch sử chat của học sinh: câu hỏi đầu tiên, gom khoảng trắng, dài quá 60 ký tự thì cắt kèm dấu ba chấm. Null nếu lượt đầu chỉ bấm nút.",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_id_updated_at",
                schema: "app",
                table: "chat_sessions",
                columns: new[] { "user_id", "updated_at" });

            // Điền tiêu đề cho phiên cũ từ câu hỏi đầu tiên của học sinh, cùng quy tắc với ChatSessionTitle.From:
            // gom khoảng trắng và ký tự điều khiển thành một dấu cách, bỏ khoảng trắng hai đầu, dài quá 60 ký tự thì cắt kèm "…".
            // Chỉ phiên chưa có tiêu đề; phiên không có tin nào của học sinh giữ null (lịch sử hiện tên mặc định)
            migrationBuilder.Sql("""
                WITH first_question AS (
                    SELECT DISTINCT ON (m.chat_session_id)
                           m.chat_session_id,
                           btrim(regexp_replace(m.content, '[[:space:][:cntrl:]]+', ' ', 'g')) AS text
                    FROM app.chat_messages m
                    WHERE m.role = 'user'
                    ORDER BY m.chat_session_id, m.created_at, m.id
                )
                UPDATE app.chat_sessions s
                SET title = CASE WHEN char_length(q.text) <= 60 THEN q.text ELSE rtrim(left(q.text, 60)) || '…' END
                FROM first_question q
                WHERE q.chat_session_id = s.id AND s.title IS NULL AND q.text <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Tiêu đề đã điền giữ nguyên: không phân biệt được tiêu đề do migration điền với tiêu đề ChatService ghi sau đó
            migrationBuilder.DropIndex(
                name: "ix_chat_sessions_user_id_updated_at",
                schema: "app",
                table: "chat_sessions");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "app",
                table: "chat_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "Tiêu đề trong lịch sử chat của học sinh: câu hỏi đầu tiên, gom khoảng trắng, dài quá 60 ký tự thì cắt kèm dấu ba chấm. Null nếu lượt đầu chỉ bấm nút.");
        }
    }
}
