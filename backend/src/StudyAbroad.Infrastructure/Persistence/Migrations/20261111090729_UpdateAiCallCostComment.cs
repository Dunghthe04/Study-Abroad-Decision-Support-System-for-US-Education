using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAiCallCostComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "cost_usd",
                schema: "app",
                table: "ai_calls",
                type: "numeric",
                nullable: true,
                comment: "Chi phí tính bằng USD, tính và lưu lúc gọi. Ollama chạy local nên 0. Claude tính theo bảng giá; model Claude chưa có trong bảng giá thì để trống (chưa biết giá, không phải miễn phí).",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "Chi phí tính bằng USD. Chỉ Claude có tiền; Ollama chạy local nên để trống hoặc 0. Chưa được tính ở giai đoạn hiện tại.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "cost_usd",
                schema: "app",
                table: "ai_calls",
                type: "numeric",
                nullable: true,
                comment: "Chi phí tính bằng USD. Chỉ Claude có tiền; Ollama chạy local nên để trống hoặc 0. Chưa được tính ở giai đoạn hiện tại.",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true,
                oldComment: "Chi phí tính bằng USD, tính và lưu lúc gọi. Ollama chạy local nên 0. Claude tính theo bảng giá; model Claude chưa có trong bảng giá thì để trống (chưa biết giá, không phải miễn phí).");
        }
    }
}
