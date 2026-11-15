using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCallPurposeChatMessageCachedTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cached_tokens",
                schema: "app",
                table: "ai_calls",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "chat_message_id",
                schema: "app",
                table: "ai_calls",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                schema: "app",
                table: "ai_calls",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_calls_chat_message_id",
                schema: "app",
                table: "ai_calls",
                column: "chat_message_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ai_calls_chat_messages_chat_message_id",
                schema: "app",
                table: "ai_calls",
                column: "chat_message_id",
                principalSchema: "app",
                principalTable: "chat_messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ai_calls_chat_messages_chat_message_id",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropIndex(
                name: "ix_ai_calls_chat_message_id",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "cached_tokens",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "chat_message_id",
                schema: "app",
                table: "ai_calls");

            migrationBuilder.DropColumn(
                name: "purpose",
                schema: "app",
                table: "ai_calls");
        }
    }
}
