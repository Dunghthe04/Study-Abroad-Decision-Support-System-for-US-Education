using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommentKnowledgeDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cột embedding nằm ngoài model EF (kiểu vector do script SQL quản lý) nên comment viết bằng SQL
            migrationBuilder.Sql("COMMENT ON COLUMN app.knowledge_documents.embedding IS 'Vector 1024 chiều của question, chat dùng để tìm dòng gần nghĩa nhất. Trống thì chat không thấy dòng này; tạo bằng script ingest_knowledge.';");

            migrationBuilder.AlterTable(
                name: "knowledge_documents",
                schema: "app",
                comment: "Kho tri thức cho chat tư vấn: mỗi dòng là một cặp câu hỏi mẫu và câu trả lời đã chốt, nhập từ file Excel (sheet knowledge_base). Chat so vector của câu hỏi học sinh với vector của question để tìm câu trả lời.");

            migrationBuilder.AlterColumn<string>(
                name: "topic_code",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                comment: "Mã thủ tục (I-20, DS-160...) nối các dòng cùng một thủ tục. Bắt buộc với dòng proc_*.",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "source_url",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                comment: "Link nguồn kiểm chứng (http, https), nhiều link cách nhau bằng dấu chấm phẩy.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "source",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                comment: "Tên nguồn bằng chữ. Cần ít nhất một trong source và source_url.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "retrieved_at",
                schema: "app",
                table: "knowledge_documents",
                type: "date",
                nullable: false,
                comment: "Ngày thu thập hoặc xem lại nguồn, để biết thông tin có cũ không.",
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "question",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: false,
                comment: "Câu hỏi mẫu. Chỉ câu này được tạo vector để tìm kiếm.",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "note",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                comment: "Ghi chú nội bộ cho người thu thập. Không đưa cho học sinh. Note bắt đầu bằng CHỜ DUYỆT thì dòng chưa chốt, import bỏ qua.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "label",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                comment: "Nhãn chủ đề LABEL_... (ví dụ LABEL_VISA_F1, LABEL_CHI_PHI) để lọc và thống kê.",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ingested_at",
                schema: "app",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Lần gần nhất tạo vector cho dòng này.",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                comment: "Link ảnh minh họa (ví dụ ảnh mẫu form). Trống nếu không có ảnh.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "entry_no",
                schema: "app",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                comment: "Cột id trong file Excel, khóa để cập nhật khi nhập lại file. Không trùng.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "embedding_hash",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                comment: "Mã băm sha256 của question lúc tạo vector, chỉ tạo lại vector khi câu hỏi đổi.",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "doc_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "Loại nội dung, một trong 13 loại: faq, glossary, proc_overview, proc_prepare, proc_steps, proc_checklist, proc_mistakes, timeline, compare, situation, data_table, resource, notice.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "answer",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: false,
                comment: "Câu trả lời chuẩn đã chốt, đưa cho AI làm căn cứ khi trả lời.",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("COMMENT ON COLUMN app.knowledge_documents.embedding IS NULL;");

            migrationBuilder.AlterTable(
                name: "knowledge_documents",
                schema: "app",
                oldComment: "Kho tri thức cho chat tư vấn: mỗi dòng là một cặp câu hỏi mẫu và câu trả lời đã chốt, nhập từ file Excel (sheet knowledge_base). Chat so vector của câu hỏi học sinh với vector của question để tìm câu trả lời.");

            migrationBuilder.AlterColumn<string>(
                name: "topic_code",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "Mã thủ tục (I-20, DS-160...) nối các dòng cùng một thủ tục. Bắt buộc với dòng proc_*.");

            migrationBuilder.AlterColumn<string>(
                name: "source_url",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Link nguồn kiểm chứng (http, https), nhiều link cách nhau bằng dấu chấm phẩy.");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Tên nguồn bằng chữ. Cần ít nhất một trong source và source_url.");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "retrieved_at",
                schema: "app",
                table: "knowledge_documents",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldComment: "Ngày thu thập hoặc xem lại nguồn, để biết thông tin có cũ không.");

            migrationBuilder.AlterColumn<string>(
                name: "question",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Câu hỏi mẫu. Chỉ câu này được tạo vector để tìm kiếm.");

            migrationBuilder.AlterColumn<string>(
                name: "note",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Ghi chú nội bộ cho người thu thập. Không đưa cho học sinh. Note bắt đầu bằng CHỜ DUYỆT thì dòng chưa chốt, import bỏ qua.");

            migrationBuilder.AlterColumn<string>(
                name: "label",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldComment: "Nhãn chủ đề LABEL_... (ví dụ LABEL_VISA_F1, LABEL_CHI_PHI) để lọc và thống kê.");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ingested_at",
                schema: "app",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Lần gần nhất tạo vector cho dòng này.");

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Link ảnh minh họa (ví dụ ảnh mẫu form). Trống nếu không có ảnh.");

            migrationBuilder.AlterColumn<int>(
                name: "entry_no",
                schema: "app",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Cột id trong file Excel, khóa để cập nhật khi nhập lại file. Không trùng.");

            migrationBuilder.AlterColumn<string>(
                name: "embedding_hash",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true,
                oldComment: "Mã băm sha256 của question lúc tạo vector, chỉ tạo lại vector khi câu hỏi đổi.");

            migrationBuilder.AlterColumn<string>(
                name: "doc_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "Loại nội dung, một trong 13 loại: faq, glossary, proc_overview, proc_prepare, proc_steps, proc_checklist, proc_mistakes, timeline, compare, situation, data_table, resource, notice.");

            migrationBuilder.AlterColumn<string>(
                name: "answer",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Câu trả lời chuẩn đã chốt, đưa cho AI làm căn cứ khi trả lời.");
        }
    }
}
