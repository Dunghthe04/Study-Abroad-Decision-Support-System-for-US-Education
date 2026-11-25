using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetainDeletedUserDataUc75 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_center_reviews_users_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropForeignKey(
                name: "fk_lead_messages_users_sender_user_id",
                schema: "app",
                table: "lead_messages");

            migrationBuilder.DropForeignKey(
                name: "fk_leads_users_user_id",
                schema: "app",
                table: "leads");

            migrationBuilder.AddColumn<Guid>(
                name: "retained_buyer_id",
                schema: "app",
                table: "payment_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "leads",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "sender_user_id",
                schema: "app",
                table: "lead_messages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "center_reviews",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "retained_buyers",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    retain_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retained_buyers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_retained_buyer_id",
                schema: "app",
                table: "payment_transactions",
                column: "retained_buyer_id");

            migrationBuilder.CreateIndex(
                name: "ix_retained_buyers_retain_until",
                schema: "app",
                table: "retained_buyers",
                column: "retain_until");

            migrationBuilder.AddForeignKey(
                name: "fk_center_reviews_users_user_id",
                schema: "app",
                table: "center_reviews",
                column: "user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_lead_messages_users_sender_user_id",
                schema: "app",
                table: "lead_messages",
                column: "sender_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_leads_users_user_id",
                schema: "app",
                table: "leads",
                column: "user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_transactions_retained_buyers_retained_buyer_id",
                schema: "app",
                table: "payment_transactions",
                column: "retained_buyer_id",
                principalSchema: "app",
                principalTable: "retained_buyers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_center_reviews_users_user_id",
                schema: "app",
                table: "center_reviews");

            migrationBuilder.DropForeignKey(
                name: "fk_lead_messages_users_sender_user_id",
                schema: "app",
                table: "lead_messages");

            migrationBuilder.DropForeignKey(
                name: "fk_leads_users_user_id",
                schema: "app",
                table: "leads");

            migrationBuilder.DropForeignKey(
                name: "fk_payment_transactions_retained_buyers_retained_buyer_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropTable(
                name: "retained_buyers",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "ix_payment_transactions_retained_buyer_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "retained_buyer_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                schema: "app",
                table: "leads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "sender_user_id",
                schema: "app",
                table: "lead_messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

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

            migrationBuilder.AddForeignKey(
                name: "fk_lead_messages_users_sender_user_id",
                schema: "app",
                table: "lead_messages",
                column: "sender_user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_leads_users_user_id",
                schema: "app",
                table: "leads",
                column: "user_id",
                principalSchema: "app",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
