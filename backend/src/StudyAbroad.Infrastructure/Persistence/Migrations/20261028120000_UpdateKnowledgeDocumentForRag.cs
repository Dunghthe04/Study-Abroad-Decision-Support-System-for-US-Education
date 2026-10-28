using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateKnowledgeDocumentForRag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "question",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "topic",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stage",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
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
                name: "quote",
                schema: "app",
                table: "knowledge_documents",
                type: "text",
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
                name: "source_note",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "review_due_at",
                schema: "app",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                schema: "app",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // BGE-M3 vector 1024 chiều phục vụ RAG B5
            migrationBuilder.Sql("ALTER TABLE app.knowledge_documents ADD COLUMN IF NOT EXISTS embedding vector(1024);");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_scope",
                schema: "app",
                table: "knowledge_documents",
                column: "scope");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_topic",
                schema: "app",
                table: "knowledge_documents",
                column: "topic");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_knowledge_documents_embedding ON app.knowledge_documents USING hnsw (embedding vector_cosine_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS app.ix_knowledge_documents_embedding;");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_topic",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_scope",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.Sql("ALTER TABLE app.knowledge_documents DROP COLUMN IF EXISTS embedding;");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "review_due_at",
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
                name: "quote",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "scope",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "stage",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "topic",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "question",
                schema: "app",
                table: "knowledge_documents");
        }
    }
}
