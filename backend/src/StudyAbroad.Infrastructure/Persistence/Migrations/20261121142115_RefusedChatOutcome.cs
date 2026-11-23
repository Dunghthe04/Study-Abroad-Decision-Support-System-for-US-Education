using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// UC-90: thêm kết quả lượt chat "refused" (hiểu câu hỏi nhưng cố ý không trả lời) và điền lại dữ liệu cũ theo luật mới của ChatTurn.Outcome:
    /// ý ngoài phạm vi loại exception (trước là no_answer) hoặc sensitive (trước là answered) chuyển thành refused.
    /// Loại con nằm ở meta_json của TIN HỎI (slm.outOfScope), nối với tin trả lời qua reply_to_message_id; chỉ tính khi lượt thật sự chạy như ngoài phạm vi
    /// (slm.intents có out_of_scope), và không đụng lượt failed. Dòng refused không còn việc cho admin nên review_status "open" về null
    /// (resolved, ignored giữ nguyên). Tin đã nằm trong một đợt duyệt (review_batch_items) giữ nguyên để đợt đó vẫn mở được.
    /// Câu "kho kiến thức không có dòng phù hợp" KHÔNG điền lại được vì dòng cũ không ghi dấu hiệu nào để phân biệt với câu trả lời bằng số liệu.
    /// </summary>
    public partial class RefusedChatOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "outcome",
                schema: "app",
                table: "chat_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                comment: "Kết quả lượt chat, hệ thống tự ghi và KHÔNG đổi sau đó: answered (trả lời được), no_answer (muốn trả lời nhưng không thể: không hiểu câu hỏi hoặc kho kiến thức không có dòng phù hợp), refused (hiểu nhưng cố ý không trả lời: ngoài chuyện du học hoặc chủ đề nhạy cảm), failed (bước AI bị lỗi). Chỉ ở tin trả lời của trợ lý, tin của học sinh để null.",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true,
                oldComment: "Kết quả lượt chat, hệ thống tự ghi và KHÔNG đổi sau đó: answered (trả lời được), no_answer (không hiểu hoặc không có dữ liệu), failed (bước AI bị lỗi). Chỉ ở tin trả lời của trợ lý, tin của học sinh để null.");

            migrationBuilder.Sql("""
                UPDATE app.chat_messages r
                SET outcome = 'refused',
                    review_status = CASE WHEN r.review_status = 'open' THEN NULL ELSE r.review_status END
                FROM app.chat_messages q
                WHERE q.id = r.reply_to_message_id
                  AND r.role = 'assistant'
                  AND r.outcome IN ('no_answer', 'answered')
                  AND q.meta_json -> 'slm' -> 'intents' @> '"out_of_scope"'::jsonb
                  AND q.meta_json -> 'slm' ->> 'outOfScope' IN ('exception', 'sensitive')
                  AND NOT EXISTS (SELECT 1 FROM app.review_batch_items i WHERE i.chat_message_id = r.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Đổi ngược theo cùng điều kiện: exception về no_answer (và open lại nếu chưa có trạng thái), sensitive về answered.
            // Không đoán được dòng nào đã được admin xử lý trước khi đổi sang refused, nên dòng exception chưa có trạng thái được mở lại
            migrationBuilder.Sql("""
                UPDATE app.chat_messages r
                SET outcome = CASE q.meta_json -> 'slm' ->> 'outOfScope' WHEN 'exception' THEN 'no_answer' ELSE 'answered' END,
                    review_status = CASE
                        WHEN q.meta_json -> 'slm' ->> 'outOfScope' = 'exception' THEN coalesce(r.review_status, 'open')
                        ELSE r.review_status END
                FROM app.chat_messages q
                WHERE q.id = r.reply_to_message_id
                  AND r.role = 'assistant'
                  AND r.outcome = 'refused'
                  AND q.meta_json -> 'slm' ->> 'outOfScope' IN ('exception', 'sensitive');
                """);

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
                oldNullable: true,
                oldComment: "Kết quả lượt chat, hệ thống tự ghi và KHÔNG đổi sau đó: answered (trả lời được), no_answer (muốn trả lời nhưng không thể: không hiểu câu hỏi hoặc kho kiến thức không có dòng phù hợp), refused (hiểu nhưng cố ý không trả lời: ngoài chuyện du học hoặc chủ đề nhạy cảm), failed (bước AI bị lỗi). Chỉ ở tin trả lời của trợ lý, tin của học sinh để null.");
        }
    }
}
