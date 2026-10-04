using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ForumPostConfiguration : IEntityTypeConfiguration<ForumPost>
{
    public void Configure(EntityTypeBuilder<ForumPost> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<ForumPost>().WithMany().HasForeignKey(x => x.ParentPostId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.StudyLevel).HasMaxLength(32);
        b.Property(x => x.Category).HasMaxLength(32);
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.HasIndex(x => new { x.ParentPostId, x.CreatedAt });
        b.HasIndex(x => new { x.StudyLevel, x.Status });
    }
}
