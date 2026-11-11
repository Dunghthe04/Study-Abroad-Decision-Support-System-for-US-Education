using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class AiCallConfiguration : IEntityTypeConfiguration<AiCall>
{
    public void Configure(EntityTypeBuilder<AiCall> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        b.Property(x => x.Purpose).HasMaxLength(64);
        b.Property(x => x.Model).HasMaxLength(64);
        b.Property(x => x.Error).HasMaxLength(1000);
        // Xóa tin nhắn thì giữ lại dòng nhật ký (số token đã tiêu thật), chỉ bỏ liên kết
        b.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.ChatMessageId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.ChatMessageId);
    }
}
