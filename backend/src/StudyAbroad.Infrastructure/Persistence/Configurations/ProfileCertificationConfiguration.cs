using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ProfileCertificationConfiguration : IEntityTypeConfiguration<ProfileCertification>
{
    public void Configure(EntityTypeBuilder<ProfileCertification> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Category).HasMaxLength(16).IsRequired();
        b.Property(x => x.TestType).HasMaxLength(32).IsRequired();
        b.Property(x => x.CustomName).HasMaxLength(128);
        b.Property(x => x.Subject).HasMaxLength(128);
        b.Property(x => x.OverallScore).HasPrecision(7, 2);
        b.Property(x => x.ScaleText).HasMaxLength(32);
        b.Property(x => x.SectionScoresJson).HasColumnType("jsonb");
        b.Property(x => x.TestDate);
        b.Property(x => x.SortOrder);
        b.ToTable(t => t.HasCheckConstraint("ck_profile_certifications_other_name",
            "test_type <> 'other' OR custom_name IS NOT NULL"));
        b.HasIndex(x => new { x.StudentProfileId, x.TestType });
    }
}
