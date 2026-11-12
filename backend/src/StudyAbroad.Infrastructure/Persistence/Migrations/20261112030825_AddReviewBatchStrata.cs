using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewBatchStrata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "strata_json",
                schema: "app",
                table: "review_batches",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]",
                comment: "Mỗi nhóm lúc bốc: tên nhóm, số câu CÓ trong nhóm (population) và số câu ĐƯỢC CHỌN (picked). Cần lưu vì bốc đều mỗi nhóm không đại diện đúng cho thực tế: tỷ lệ đúng chung phải nhân trọng số theo population.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "strata_json",
                schema: "app",
                table: "review_batches");
        }
    }
}
