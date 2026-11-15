using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class UniversityOfferingMajorConfiguration : IEntityTypeConfiguration<UniversityOfferingMajor>
{
    public void Configure(EntityTypeBuilder<UniversityOfferingMajor> b)
    {
        b.HasKey(x => new { x.UniversityOfferingId, x.MajorId });
        b.HasOne<UniversityOffering>().WithMany().HasForeignKey(x => x.UniversityOfferingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Major>().WithMany().HasForeignKey(x => x.MajorId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.IsStem).HasDefaultValue(false);
        b.Property(x => x.IsVerified).HasDefaultValue(false);
        b.HasIndex(x => x.MajorId);
    }
}
