using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StudyAbroad.Infrastructure.Persistence;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <summary>Mẫu SLM chỉ gồm câu hỏi, hồ sơ, intents...; câu trả lời đã chuyển sang kho tri thức nên bỏ hai cột answer và topic.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261119103000_DropSlmSampleAnswerTopic")]
    public partial class DropSlmSampleAnswerTopic : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "answer", schema: "app", table: "slm_training_samples");
            migrationBuilder.DropColumn(name: "topic", schema: "app", table: "slm_training_samples");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "answer", schema: "app", table: "slm_training_samples", type: "text", nullable: true,
                comment: "Câu trả lời mẫu. Dòng cũ chỉ có intents thì để trống.");
            migrationBuilder.AddColumn<string>(name: "topic", schema: "app", table: "slm_training_samples", type: "character varying(128)", maxLength: 128, nullable: true,
                comment: "Chủ đề.");
        }
    }
}
