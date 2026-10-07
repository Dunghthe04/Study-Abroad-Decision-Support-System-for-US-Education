using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class UniversityOfferingConfiguration : IEntityTypeConfiguration<UniversityOffering>
{
    public void Configure(EntityTypeBuilder<UniversityOffering> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<University>().WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.StudyLevel).HasMaxLength(32).IsRequired();
        b.Property(x => x.Majors).HasColumnType("text[]");
        b.Property(x => x.StemMajors).HasColumnType("text[]");
        b.Property(x => x.AcademicYear).HasMaxLength(16);
        b.Property(x => x.TuitionUsd).HasPrecision(12, 2);
        b.Property(x => x.LivingUsd).HasPrecision(12, 2);
        b.Property(x => x.FeesUsd).HasPrecision(12, 2);
        b.Property(x => x.MinGpa4).HasPrecision(3, 2);
        b.Property(x => x.MinIelts).HasPrecision(3, 1);
        b.Property(x => x.MinToefl).HasPrecision(5, 1);
        b.Property(x => x.MinDuolingo).HasPrecision(5, 1);
        b.Property(x => x.AvgGpa4).HasPrecision(3, 2);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_university_offerings_avg_gpa4", "avg_gpa4 IS NULL OR avg_gpa4 BETWEEN 0 AND 4");
            t.HasCheckConstraint("ck_university_offerings_sat_range",
                "(sat25 IS NULL OR sat25 BETWEEN 400 AND 1600) AND (sat75 IS NULL OR sat75 BETWEEN 400 AND 1600) "
                + "AND (sat25 IS NULL OR sat75 IS NULL OR sat25 <= sat75)");
        });
        b.Property(x => x.SatPolicy).HasMaxLength(16);
        b.Property(x => x.DeadlinesJson).HasColumnType("jsonb");
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.SourceUrl).HasMaxLength(512);
        b.HasIndex(x => new { x.UniversityId, x.StudyLevel }).IsUnique();
    }
}
