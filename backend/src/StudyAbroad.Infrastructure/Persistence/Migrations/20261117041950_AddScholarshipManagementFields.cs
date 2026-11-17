using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScholarshipManagementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_scholarships_university_id",
                schema: "app",
                table: "scholarships");

            migrationBuilder.AddColumn<Guid>(
                name: "last_edited_by_user_id",
                schema: "app",
                table: "scholarships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_reliability",
                schema: "app",
                table: "scholarships",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_scholarships_last_edited_by_user_id",
                schema: "app",
                table: "scholarships",
                column: "last_edited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_scholarships_university_id_is_active",
                schema: "app",
                table: "scholarships",
                columns: new[] { "university_id", "is_active" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_scholarships_owner",
                schema: "app",
                table: "scholarships",
                sql: "university_id IS NOT NULL OR provider IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_scholarships_source_reliability",
                schema: "app",
                table: "scholarships",
                sql: "source_reliability IS NULL OR source_reliability IN ('official', 'aggregated', 'self_reported')");

            migrationBuilder.AddForeignKey(
                name: "fk_scholarships_users_last_edited_by_user_id",
                schema: "app",
                table: "scholarships",
                column: "last_edited_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_scholarships_users_last_edited_by_user_id",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropIndex(
                name: "ix_scholarships_last_edited_by_user_id",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropIndex(
                name: "ix_scholarships_university_id_is_active",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_scholarships_owner",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_scholarships_source_reliability",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "last_edited_by_user_id",
                schema: "app",
                table: "scholarships");

            migrationBuilder.DropColumn(
                name: "source_reliability",
                schema: "app",
                table: "scholarships");

            migrationBuilder.CreateIndex(
                name: "ix_scholarships_university_id",
                schema: "app",
                table: "scholarships",
                column: "university_id");
        }
    }
}
