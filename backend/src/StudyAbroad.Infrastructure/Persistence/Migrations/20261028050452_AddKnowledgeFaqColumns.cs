using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeFaqColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_status_study_level_doc_type",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "doc_type",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "ingested_at",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.AddColumn<DateTime>(
                name: "review_due_at",
                schema: "app",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "study_level",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "general",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

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

            migrationBuilder.AddColumn<string>(
                name: "question",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "quote",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scope",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "system_advisory");

            migrationBuilder.AddColumn<string>(
                name: "source_note",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "official");

            migrationBuilder.AddColumn<string>(
                name: "topic",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_code",
                schema: "app",
                table: "knowledge_documents",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_status_scope_study_level",
                schema: "app",
                table: "knowledge_documents",
                columns: new[] { "status", "scope", "study_level" });

            // Cột vector do advisor dùng để tìm kiếm; EF không biết kiểu vector nên thêm bằng SQL thô
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
            migrationBuilder.Sql("ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS embedding vector(1024);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_knowledge_documents_embedding " +
                "ON app.knowledge_documents USING hnsw (embedding vector_cosine_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ix_knowledge_documents_embedding;");
            migrationBuilder.Sql("ALTER TABLE app.knowledge_documents DROP COLUMN IF EXISTS embedding;");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_code",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_status_scope_study_level",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "code",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "embedding_hash",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "question",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "quote",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "scope",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "source_note",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "source_type",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "topic",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "review_due_at",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.AddColumn<DateTime>(
                name: "ingested_at",
                schema: "app",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "study_level",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "general");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft");

            migrationBuilder.AddColumn<string>(
                name: "doc_type",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_status_study_level_doc_type",
                schema: "app",
                table: "knowledge_documents",
                columns: new[] { "status", "study_level", "doc_type" });
        }
    }
}
