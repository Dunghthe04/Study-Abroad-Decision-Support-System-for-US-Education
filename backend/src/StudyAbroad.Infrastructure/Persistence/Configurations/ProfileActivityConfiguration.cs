using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ProfileActivityConfiguration : IEntityTypeConfiguration<ProfileActivity>
{
    public void Configure(EntityTypeBuilder<ProfileActivity> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.Organization).HasMaxLength(256);
        b.Property(x => x.Role).HasMaxLength(128);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.HasIndex(x => new { x.StudentProfileId, x.Kind });
    }
}
