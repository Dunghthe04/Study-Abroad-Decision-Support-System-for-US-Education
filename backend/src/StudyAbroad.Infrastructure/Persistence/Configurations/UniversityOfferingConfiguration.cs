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

        // Số liệu cho công thức gợi ý trường. Tên cột trùng với khóa trong file Excel thu thập dữ liệu.
        b.Property(x => x.IntlAcceptanceRate).HasPrecision(5, 4);
        b.Property(x => x.C7GpaImportance).HasMaxLength(16).HasColumnName("c7_gpa_importance");
        b.Property(x => x.C7TestImportance).HasMaxLength(16).HasColumnName("c7_test_importance");
        b.Property(x => x.C7EcImportance).HasMaxLength(16).HasColumnName("c7_ec_importance");
        b.Property(x => x.SatSubmitPct).HasPrecision(5, 4);
        b.Property(x => x.GpaPct40).HasPrecision(5, 4).HasColumnName("gpa_pct_4_0");
        b.Property(x => x.GpaPct375To399).HasPrecision(5, 4).HasColumnName("gpa_pct_375_399");
        b.Property(x => x.GpaPct350To374).HasPrecision(5, 4).HasColumnName("gpa_pct_350_374");
        b.Property(x => x.GpaPct325To349).HasPrecision(5, 4).HasColumnName("gpa_pct_325_349");
        b.Property(x => x.GpaPct300To324).HasPrecision(5, 4).HasColumnName("gpa_pct_300_324");
        b.Property(x => x.GpaPct250To299).HasPrecision(5, 4).HasColumnName("gpa_pct_250_299");
        b.Property(x => x.GpaPctBelow250).HasPrecision(5, 4).HasColumnName("gpa_pct_below_250");
        b.Property(x => x.IntlAidPolicy).HasMaxLength(16);
        b.Property(x => x.IntlAidAvgUsd).HasPrecision(12, 2);
        b.Property(x => x.RetentionRate1Yr).HasPrecision(5, 4).HasColumnName("retention_rate_1yr");
        b.Property(x => x.GradRate6Yr).HasPrecision(5, 4).HasColumnName("grad_rate_6yr");
        b.Property(x => x.DeadlinesJson).HasColumnType("jsonb");
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.SourceUrl).HasMaxLength(512);
        b.HasIndex(x => new { x.UniversityId, x.StudyLevel }).IsUnique();
    }
}
