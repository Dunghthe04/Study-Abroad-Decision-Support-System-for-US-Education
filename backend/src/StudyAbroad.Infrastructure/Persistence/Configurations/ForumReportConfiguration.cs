using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ForumReportConfiguration : IEntityTypeConfiguration<ForumReport>
{
    public void Configure(EntityTypeBuilder<ForumReport> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<ForumPost>().WithMany().HasForeignKey(x => x.ForumPostId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.HandledByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Reason).HasMaxLength(64).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(1000);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.Status);
    }
}
