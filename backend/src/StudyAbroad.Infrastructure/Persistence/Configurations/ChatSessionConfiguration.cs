using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> b)
    {
        b.ToTable("chat_sessions", t => t.HasComment(
            "Một cuộc chat của một học sinh. Các tin nhắn nằm ở bảng chat_messages (nối bằng chat_session_id)."));

        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.UserId).HasComment("Chủ cuộc chat. Xóa tài khoản thì xóa luôn cuộc chat và các tin nhắn.");
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.StudyLevel).HasMaxLength(32).HasComment("Bậc học học sinh đang hỏi về.");
        b.Property(x => x.CurrentStep).HasMaxLength(8).HasComment("Bước hiện tại của hội thoại (B0..B6), cập nhật sau mỗi lượt.");
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.Property(x => x.StateJson).HasColumnType("jsonb")
            .HasComment("Trạng thái hội thoại advisor trả về sau mỗi lượt, gửi lại ở lượt sau. Nội bộ, không trả cho trình duyệt.");
    }
}
