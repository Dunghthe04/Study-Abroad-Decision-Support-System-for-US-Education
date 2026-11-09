using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropKnowledgeTitleStageRenameContentToAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stage",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "app",
                table: "knowledge_documents");

            migrationBuilder.RenameColumn(
                name: "content",
                schema: "app",
                table: "knowledge_documents",
                newName: "answer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "answer",
                schema: "app",
                table: "knowledge_documents",
                newName: "content");

            migrationBuilder.AddColumn<string>(
                name: "stage",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "app",
                table: "knowledge_documents",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }
    }
}
