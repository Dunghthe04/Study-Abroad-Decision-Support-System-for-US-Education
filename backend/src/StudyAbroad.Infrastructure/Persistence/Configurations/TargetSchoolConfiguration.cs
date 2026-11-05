using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class TargetSchoolConfiguration : IEntityTypeConfiguration<TargetSchool>
{
    public void Configure(EntityTypeBuilder<TargetSchool> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<University>().WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Category).HasMaxLength(16);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => new { x.UserId, x.UniversityId }).IsUnique();
    }
}
