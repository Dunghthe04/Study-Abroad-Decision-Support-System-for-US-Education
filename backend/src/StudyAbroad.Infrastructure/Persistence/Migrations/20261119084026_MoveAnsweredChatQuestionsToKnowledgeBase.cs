using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// UC-53: câu chat chưa trả lời được trước đây nằm trong mẫu SLM (source = chat-unanswered). Câu nào moderator đã viết
    /// câu trả lời thì chuyển sang kho tri thức ở dạng nháp và xóa mềm bản bên SLM. Câu chưa có câu trả lời giữ nguyên bên SLM.
    /// </summary>
    public partial class MoveAnsweredChatQuestionsToKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH candidates AS (
                    SELECT DISTINCT ON (lower(q.question)) q.question, left(btrim(s.answer), 4000) AS answer, s.created_at
                    FROM app.slm_training_samples s
                    CROSS JOIN LATERAL (SELECT left(regexp_replace(btrim(s.question), '\s+', ' ', 'g'), 500) AS question) q
                    WHERE s.source = 'chat-unanswered'
                      AND s.deleted_at IS NULL
                      AND coalesce(btrim(s.answer), '') <> ''
                    ORDER BY lower(q.question), s.created_at
                ),
                moved AS (
                    INSERT INTO app.knowledge_base_entries (id, question, answer, origin, review_status, levels, created_at)
                    SELECT gen_random_uuid(), c.question, c.answer, 'chat-unanswered', 'draft', '{}', c.created_at
                    FROM candidates c
                    WHERE NOT EXISTS (
                        SELECT 1 FROM app.knowledge_base_entries k WHERE lower(k.question) = lower(c.question))
                    RETURNING id
                )
                INSERT INTO app.knowledge_base_entry_history (id, entry_id, action, to_status, note, created_at)
                SELECT gen_random_uuid(), id, 'migrate', 'draft', 'Chuyển từ mẫu SLM (câu chat chưa trả lời)', now()
                FROM moved;

                UPDATE app.slm_training_samples
                SET deleted_at = now()
                WHERE source = 'chat-unanswered'
                  AND deleted_at IS NULL
                  AND coalesce(btrim(answer), '') <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chuyển dữ liệu một chiều: không đoán được dòng nào đã bị sửa sau khi chuyển, nên Down không làm gì
        }
    }
}