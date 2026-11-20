using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCenterLegalHoldTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "legal_hold_at",
                schema: "app",
                table: "study_centers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "legal_hold_by_user_id",
                schema: "app",
                table: "study_centers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_study_centers_legal_hold_by_user_id",
                schema: "app",
                table: "study_centers",
                column: "legal_hold_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_study_centers_users_legal_hold_by_user_id",
                schema: "app",
                table: "study_centers",
                column: "legal_hold_by_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_study_centers_users_legal_hold_by_user_id",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropIndex(
                name: "ix_study_centers_legal_hold_by_user_id",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "legal_hold_at",
                schema: "app",
                table: "study_centers");

            migrationBuilder.DropColumn(
                name: "legal_hold_by_user_id",
                schema: "app",
                table: "study_centers");
        }
    }
}
