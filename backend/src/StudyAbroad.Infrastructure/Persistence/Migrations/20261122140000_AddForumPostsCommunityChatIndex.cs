using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyAbroad.Infrastructure.Persistence;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261122140000_AddForumPostsCommunityChatIndex")]
public partial class AddForumPostsCommunityChatIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_forum_posts_category_status_created_at",
            schema: "app",
            table: "forum_posts",
            columns: new[] { "category", "status", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_forum_posts_category_status_created_at",
            schema: "app",
            table: "forum_posts");
    }
}
