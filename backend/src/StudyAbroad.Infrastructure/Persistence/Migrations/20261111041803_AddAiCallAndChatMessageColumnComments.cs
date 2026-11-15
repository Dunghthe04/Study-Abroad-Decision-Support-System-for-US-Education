using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCallAndChatMessageColumnComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "chat_messages",
                schema: "app",
                comment: "Tin nhắn của cuộc chat: tin của học sinh và tin trả lời của trợ lý. Chứa nội dung chat nên là dữ liệu cá nhân (GPA, tài chính, có thể là học sinh vị thành niên): chỉ admin và moderator được xem.");

            migrationBuilder.AlterTable(
                name: "ai_calls",
                schema: "app",
                comment: "Nhật ký MỖI LẦN gọi AI (Claude hoặc Ollama). Một lượt chat gồm vài lần gọi, mỗi lần một dòng. Để admin xem token, tốc độ, lỗi theo tính năng và theo tin nhắn. Chỉ chứa số đo, không chứa nội dung chat.");

            migrationBuilder.AlterColumn<string>(
                name: "sources_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                comment: "Nguồn trích dẫn của câu trả lời (title, url).",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "app",
                table: "chat_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                comment: "user (học sinh), assistant (trợ lý) hoặc system.",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "review_status",
                schema: "app",
                table: "chat_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                comment: "Admin đã xử lý chưa. Chỉ dùng khi outcome là no_answer hoặc failed: open (mới, chưa xem), resolved (đã xử lý, ví dụ bổ sung tài liệu), ignored (xem rồi, bỏ qua). Null nếu hệ thống trả lời được.",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                schema: "app",
                table: "chat_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                comment: "Kết quả lượt chat, hệ thống tự ghi và KHÔNG đổi sau đó: answered (trả lời được), no_answer (không hiểu hoặc không có dữ liệu), failed (bước AI bị lỗi). Chỉ ở tin trả lời của trợ lý, tin của học sinh để null.",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "meta_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                comment: "Tóm tắt lượt cho admin (JSON): intents (các ý SLM tách), actions, slmFallback, confidence, totalLatencyMs, totalTokens. Chỉ ở tin trả lời của trợ lý. Lọc được vào từng khóa, ví dụ meta_json ->> 'slmFallback' = 'true'.",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "content",
                schema: "app",
                table: "chat_messages",
                type: "text",
                nullable: false,
                comment: "Nội dung tin nhắn.",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "ai_calls",
                type: "uuid",
                nullable: true,
                comment: "Học sinh gây ra lần gọi. Null nếu gọi ngoài phiên đăng nhập.",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "total_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                comment: "Token vào cộng token ra.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "success",
                schema: "app",
                table: "ai_calls",
                type: "boolean",
                nullable: false,
                comment: "False nếu lần gọi lỗi (quá tải, hết thời gian, bị từ chối). Khi đó token bằng 0 và cột error có lý do.",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "skill_version",
                schema: "app",
                table: "ai_calls",
                type: "text",
                nullable: true,
                comment: "Số phiên bản ở dòng đầu file skill (ví dụ 3.0). Sửa nội dung skill thì phải tăng số này, nếu không không so sánh được trước và sau.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "purpose",
                schema: "app",
                table: "ai_calls",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                comment: "Việc của lần gọi = tên file skill (slm_intents, skill_tra_loi...). Rỗng với embedding. Dùng để báo cáo token theo tính năng.",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "prompt_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                comment: "Token VÀO: phần gửi lên AI. Với Claude, chưa gồm phần đọc từ cache (xem cached_tokens).",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "model",
                schema: "app",
                table: "ai_calls",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                comment: "Tên model đã gọi, ví dụ claude-haiku-5-5 hoặc usas-slm-v4.",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "latency_ms",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                comment: "Thời gian của lần gọi, tính bằng mili giây.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                schema: "app",
                table: "ai_calls",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                comment: "Nguồn gọi: claude (trả phí theo token) hoặc ollama (model chạy trên máy mình, không tốn tiền).",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "error",
                schema: "app",
                table: "ai_calls",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                comment: "Lý do lỗi ngắn, chỉ có khi success = false.",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "cost_usd",
                schema: "app",
                table: "ai_calls",
                type: "numeric",
                nullable: true,
                comment: "Chi phí tính bằng USD. Chỉ Claude có tiền; Ollama chạy local nên để trống hoặc 0. Chưa được tính ở giai đoạn hiện tại.",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "completion_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                comment: "Token RA: phần AI viết trả về.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "chat_message_id",
                schema: "app",
                table: "ai_calls",
                type: "uuid",
                nullable: true,
                comment: "Tin TRẢ LỜI của trợ lý do lần gọi này tạo ra. Một tin có nhiều lần gọi. Xóa tin thì giữ dòng này (token đã tiêu thật), chỉ bỏ liên kết.",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "cached_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                comment: "Token Claude đọc từ cache, tính tiền khác token thường. Luôn 0 với Ollama.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "chat_messages",
                schema: "app",
                oldComment: "Tin nhắn của cuộc chat: tin của học sinh và tin trả lời của trợ lý. Chứa nội dung chat nên là dữ liệu cá nhân (GPA, tài chính, có thể là học sinh vị thành niên): chỉ admin và moderator được xem.");

            migrationBuilder.AlterTable(
                name: "ai_calls",
                schema: "app",
                oldComment: "Nhật ký MỖI LẦN gọi AI (Claude hoặc Ollama). Một lượt chat gồm vài lần gọi, mỗi lần một dòng. Để admin xem token, tốc độ, lỗi theo tính năng và theo tin nhắn. Chỉ chứa số đo, không chứa nội dung chat.");

            migrationBuilder.AlterColumn<string>(
                name: "sources_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "Nguồn trích dẫn của câu trả lời (title, url).");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "app",
                table: "chat_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldComment: "user (học sinh), assistant (trợ lý) hoặc system.");

            migrationBuilder.AlterColumn<string>(
                name: "review_status",
                schema: "app",
                table: "chat_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true,
                oldComment: "Admin đã xử lý chưa. Chỉ dùng khi outcome là no_answer hoặc failed: open (mới, chưa xem), resolved (đã xử lý, ví dụ bổ sung tài liệu), ignored (xem rồi, bỏ qua). Null nếu hệ thống trả lời được.");

            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                schema: "app",
                table: "chat_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true,
                oldComment: "Kết quả lượt chat, hệ thống tự ghi và KHÔNG đổi sau đó: answered (trả lời được), no_answer (không hiểu hoặc không có dữ liệu), failed (bước AI bị lỗi). Chỉ ở tin trả lời của trợ lý, tin của học sinh để null.");

            migrationBuilder.AlterColumn<string>(
                name: "meta_json",
                schema: "app",
                table: "chat_messages",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true,
                oldComment: "Tóm tắt lượt cho admin (JSON): intents (các ý SLM tách), actions, slmFallback, confidence, totalLatencyMs, totalTokens. Chỉ ở tin trả lời của trợ lý. Lọc được vào từng khóa, ví dụ meta_json ->> 'slmFallback' = 'true'.");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                schema: "app",
                table: "chat_messages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Nội dung tin nhắn.");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "ai_calls",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "Học sinh gây ra lần gọi. Null nếu gọi ngoài phiên đăng nhập.");

            migrationBuilder.AlterColumn<int>(
                name: "total_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Token vào cộng token ra.");

            migrationBuilder.AlterColumn<bool>(
                name: "success",
                schema: "app",
                table: "ai_calls",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "False nếu lần gọi lỗi (quá tải, hết thời gian, bị từ chối). Khi đó token bằng 0 và cột error có lý do.");

            migrationBuilder.AlterColumn<string>(
                name: "skill_version",
                schema: "app",
                table: "ai_calls",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Số phiên bản ở dòng đầu file skill (ví dụ 3.0). Sửa nội dung skill thì phải tăng số này, nếu không không so sánh được trước và sau.");

            migrationBuilder.AlterColumn<string>(
                name: "purpose",
                schema: "app",
                table: "ai_calls",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "Việc của lần gọi = tên file skill (slm_intents, skill_tra_loi...). Rỗng với embedding. Dùng để báo cáo token theo tính năng.");

            migrationBuilder.AlterColumn<int>(
                name: "prompt_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Token VÀO: phần gửi lên AI. Với Claude, chưa gồm phần đọc từ cache (xem cached_tokens).");

            migrationBuilder.AlterColumn<string>(
                name: "model",
                schema: "app",
                table: "ai_calls",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "Tên model đã gọi, ví dụ claude-haiku-5-5 hoặc usas-slm-v4.");

            migrationBuilder.AlterColumn<int>(
                name: "latency_ms",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Thời gian của lần gọi, tính bằng mili giây.");

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                schema: "app",
                table: "ai_calls",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldComment: "Nguồn gọi: claude (trả phí theo token) hoặc ollama (model chạy trên máy mình, không tốn tiền).");

            migrationBuilder.AlterColumn<string>(
                name: "error",
                schema: "app",
                table: "ai_calls",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true,
                oldComment: "Lý do lỗi ngắn, chỉ có khi success = false.");

            migrationBuilder.AlterColumn<decimal>(
                name: "cost_usd",
                schema: "app",
                table: "ai_calls",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "Chi phí tính bằng USD. Chỉ Claude có tiền; Ollama chạy local nên để trống hoặc 0. Chưa được tính ở giai đoạn hiện tại.");

            migrationBuilder.AlterColumn<int>(
                name: "completion_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Token RA: phần AI viết trả về.");

            migrationBuilder.AlterColumn<Guid>(
                name: "chat_message_id",
                schema: "app",
                table: "ai_calls",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "Tin TRẢ LỜI của trợ lý do lần gọi này tạo ra. Một tin có nhiều lần gọi. Xóa tin thì giữ dòng này (token đã tiêu thật), chỉ bỏ liên kết.");

            migrationBuilder.AlterColumn<int>(
                name: "cached_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Token Claude đọc từ cache, tính tiền khác token thường. Luôn 0 với Ollama.");
        }
    }
}
