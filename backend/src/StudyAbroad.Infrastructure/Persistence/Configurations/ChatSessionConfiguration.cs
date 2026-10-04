using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Skill>().WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.StudyLevel).HasMaxLength(32);
        b.Property(x => x.CurrentStep).HasMaxLength(8);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
