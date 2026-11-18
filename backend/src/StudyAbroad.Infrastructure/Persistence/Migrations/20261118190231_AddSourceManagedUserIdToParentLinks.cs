using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceManagedUserIdToParentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_managed_user_id",
                schema: "app",
                table: "parent_student_links",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_parent_student_links_source_managed_user_id",
                schema: "app",
                table: "parent_student_links",
                column: "source_managed_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_parent_student_links_users_source_managed_user_id",
                schema: "app",
                table: "parent_student_links",
                column: "source_managed_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_parent_student_links_users_source_managed_user_id",
                schema: "app",
                table: "parent_student_links");

            migrationBuilder.DropIndex(
                name: "ix_parent_student_links_source_managed_user_id",
                schema: "app",
                table: "parent_student_links");

            migrationBuilder.DropColumn(
                name: "source_managed_user_id",
                schema: "app",
                table: "parent_student_links");
        }
    }
}
