using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyAbroad.Infrastructure.Persistence;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>Hai bảng lịch sử chỉ thêm dòng, không bao giờ sửa nên bỏ updated_at; split chỉ có 3 giá trị nên index không giúp gì.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261119113000_TrimHistoryUpdatedAtAndSplitIndex")]
    public partial class TrimHistoryUpdatedAtAndSplitIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "updated_at", schema: "app", table: "slm_sample_history");
            migrationBuilder.DropColumn(name: "updated_at", schema: "app", table: "knowledge_base_entry_history");
            migrationBuilder.DropIndex(name: "ix_slm_training_samples_split", schema: "app", table: "slm_training_samples");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "updated_at", schema: "app", table: "slm_sample_history", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "updated_at", schema: "app", table: "knowledge_base_entry_history", type: "timestamp with time zone", nullable: true);
            migrationBuilder.CreateIndex(name: "ix_slm_training_samples_split", schema: "app", table: "slm_training_samples", column: "split");
        }
    }
}
