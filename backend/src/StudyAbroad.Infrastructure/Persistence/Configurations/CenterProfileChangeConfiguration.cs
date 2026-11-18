using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class CenterProfileChangeConfiguration : IEntityTypeConfiguration<CenterProfileChange>
{
    public void Configure(EntityTypeBuilder<CenterProfileChange> b)
    {
        b.ToTable("center_profile_changes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Field).HasMaxLength(32).IsRequired();
        b.Property(x => x.OldValue).HasMaxLength(1024);
        b.Property(x => x.NewValue).HasMaxLength(1024);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired().HasDefaultValue("pending");
        b.Property(x => x.ReviewReason).HasMaxLength(1000);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.StudyCenterId);
        b.HasOne<StudyCenter>().WithMany().HasForeignKey(x => x.StudyCenterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
