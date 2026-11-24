using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyAbroad.Infrastructure.Persistence;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261123030000_AddForumPostAttachmentAndReply")]
public partial class AddForumPostAttachmentAndReply : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "attachment_url",
            schema: "app",
            table: "forum_posts",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "attachment_public_id",
            schema: "app",
            table: "forum_posts",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "reply_to_post_id",
            schema: "app",
            table: "forum_posts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_forum_posts_reply_to_post_id",
            schema: "app",
            table: "forum_posts",
            column: "reply_to_post_id");

        migrationBuilder.AddForeignKey(
            name: "fk_forum_posts_forum_posts_reply_to_post_id",
            schema: "app",
            table: "forum_posts",
            column: "reply_to_post_id",
            principalSchema: "app",
            principalTable: "forum_posts",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_forum_posts_forum_posts_reply_to_post_id",
            schema: "app",
            table: "forum_posts");

        migrationBuilder.DropIndex(
            name: "ix_forum_posts_reply_to_post_id",
            schema: "app",
            table: "forum_posts");

        migrationBuilder.DropColumn(
            name: "attachment_url",
            schema: "app",
            table: "forum_posts");

        migrationBuilder.DropColumn(
            name: "attachment_public_id",
            schema: "app",
            table: "forum_posts");

        migrationBuilder.DropColumn(
            name: "reply_to_post_id",
            schema: "app",
            table: "forum_posts");
    }
}
