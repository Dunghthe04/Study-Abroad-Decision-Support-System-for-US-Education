using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// [UC-75 lớp 2] Bỏ bảng retained_buyers (trùng với deleted_buyer_retentions của AlignCodeWithUcSpec).
    /// Họ tên/email người mua của tài khoản đã xóa hẳn chỉ còn lưu ở deleted_buyer_retentions.
    /// </summary>
    public partial class DropDuplicateRetainedBuyers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payment_transactions_retained_buyers_retained_buyer_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropIndex(
                name: "ix_payment_transactions_retained_buyer_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "retained_buyer_id",
                schema: "app",
                table: "payment_transactions");

            migrationBuilder.DropTable(
                name: "retained_buyers",
                schema: "app");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "ix_retained_buyers_retain_until",
                schema: "app",
                table: "retained_buyers",
                column: "retain_until");

            migrationBuilder.AddColumn<Guid>(
                name: "retained_buyer_id",
                schema: "app",
                table: "payment_transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_retained_buyer_id",
                schema: "app",
                table: "payment_transactions",
                column: "retained_buyer_id");

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
    }
}
