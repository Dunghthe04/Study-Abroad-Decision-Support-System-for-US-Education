using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KeepPaymentsAndReportsOnUserPurge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_forum_reports_users_reporter_user_id",
                schema: "app",
                table: "forum_reports");

            migrationBuilder.DropForeignKey(
                name: "fk_payment_transactions_users_user_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "payment_transactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "reporter_user_id",
                schema: "app",
                table: "forum_reports",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "fk_forum_reports_users_reporter_user_id",
                schema: "app",
                table: "forum_reports",
                column: "reporter_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_transactions_users_user_id",
                schema: "app",
                table: "payment_transactions",
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
                name: "fk_forum_reports_users_reporter_user_id",
                schema: "app",
                table: "forum_reports");

            migrationBuilder.DropForeignKey(
                name: "fk_payment_transactions_users_user_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "payment_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "reporter_user_id",
                schema: "app",
                table: "forum_reports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_forum_reports_users_reporter_user_id",
                schema: "app",
                table: "forum_reports",
                column: "reporter_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_transactions_users_user_id",
                schema: "app",
                table: "payment_transactions",
                column: "user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
