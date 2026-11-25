using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCenterVerificationAndReviewSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_center_reviews_users_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.AddColumn<bool>(
                name: "is_service_contract_valid",
                schema: "app",
                table: "center_verification_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "center_reviews",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "fk_center_reviews_users_user_id",
                schema: "app",
                table: "center_reviews",
                column: "user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_center_reviews_users_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropColumn(
                name: "is_service_contract_valid",
                schema: "app",
                table: "center_verification_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "center_reviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_center_reviews_users_user_id",
                schema: "app",
                table: "center_reviews",
                column: "user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
