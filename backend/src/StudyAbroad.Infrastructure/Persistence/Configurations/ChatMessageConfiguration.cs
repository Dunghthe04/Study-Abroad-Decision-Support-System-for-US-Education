using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<ChatSession>().WithMany().HasForeignKey(x => x.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Role).HasMaxLength(16).IsRequired();
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.Step).HasMaxLength(8);
        b.Property(x => x.SourcesJson).HasColumnType("jsonb");
        b.Property(x => x.SuggestedCentersJson).HasColumnType("jsonb");
        b.Property(x => x.ReviewStatus).HasMaxLength(16);
        b.HasIndex(x => new { x.ChatSessionId, x.CreatedAt });
        b.HasIndex(x => x.ReviewStatus);
    }
}
