using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParentLinkRevokeToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "revoke_token_hash",
                schema: "app",
                table: "parent_student_links",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_parent_student_links_revoke_token_hash",
                schema: "app",
                table: "parent_student_links",
                column: "revoke_token_hash",
                unique: true,
                filter: "revoke_token_hash IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_parent_student_links_revoke_token_hash",
                schema: "app",
                table: "parent_student_links");

            migrationBuilder.DropColumn(
                name: "revoke_token_hash",
                schema: "app",
                table: "parent_student_links");
        }
    }
}
