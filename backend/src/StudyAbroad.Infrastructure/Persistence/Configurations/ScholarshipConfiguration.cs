using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ScholarshipConfiguration : IEntityTypeConfiguration<Scholarship>
{
    public void Configure(EntityTypeBuilder<Scholarship> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<University>().WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.Provider).HasMaxLength(256);
        b.Property(x => x.StudyLevel).HasMaxLength(32).IsRequired();
        b.Property(x => x.AmountUsd).HasPrecision(12, 2);
        b.Property(x => x.CoverageType).HasMaxLength(32);
        b.Property(x => x.MinGpa4).HasPrecision(3, 2);
        b.Property(x => x.MinIelts).HasPrecision(3, 1);
        b.Property(x => x.EligibilityNotes).HasMaxLength(2000);
        b.Property(x => x.SourceUrl).HasMaxLength(512);
        b.HasIndex(x => x.StudyLevel);
    }
}
