using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bổ sung cho UpdateKnowledgeDocumentForRag: mã cố định KB-001 (khóa để nạp lại dữ liệu mà không nhân đôi dòng)
    /// và mã băm của câu hỏi (chỉ tạo lại vector khi câu hỏi đổi).
    /// </summary>
    public partial class AddKnowledgeCodeAndEmbeddingHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "embedding_hash",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_code",
                schema: "app",
                table: "knowledge_documents",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_code",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "embedding_hash",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "code",
                schema: "app",
                table: "knowledge_documents");
        }
    }
}
