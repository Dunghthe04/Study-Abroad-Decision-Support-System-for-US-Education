using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.TargetLevel).HasMaxLength(32).IsRequired();
        b.Property(x => x.CurrentSchool).HasMaxLength(256);
        b.Property(x => x.CurrentGrade).HasMaxLength(64);
        b.Property(x => x.GradeScale).HasMaxLength(8).IsRequired();
        b.Property(x => x.OverallGpa).HasPrecision(5, 2);
        b.Property(x => x.IntendedMajor).HasMaxLength(128);
        b.Property(x => x.Ielts).HasPrecision(3, 1);
        b.Property(x => x.Toefl).HasPrecision(5, 1);
        b.Property(x => x.Duolingo).HasPrecision(5, 1);
        b.Property(x => x.Sat).HasPrecision(5, 1);
        b.Property(x => x.Act).HasPrecision(4, 1);
        b.Property(x => x.Gre).HasPrecision(5, 1);
        b.Property(x => x.Gmat).HasPrecision(5, 1);
        b.Property(x => x.OtherTestsJson).HasColumnType("jsonb");
        b.Property(x => x.ExtracurricularScore).HasPrecision(4, 2);
        b.ToTable(t => t.HasCheckConstraint("ck_student_profiles_extracurricular_score",
            "extracurricular_score IS NULL OR extracurricular_score BETWEEN 0 AND 4"));
        b.Property(x => x.AnnualBudgetUsd).HasPrecision(12, 2);
        b.Property(x => x.FundingSource).HasMaxLength(32);
        b.Property(x => x.PreferredStates).HasColumnType("text[]");
        b.Property(x => x.RoadmapProgressJson).HasColumnType("jsonb");
        b.HasIndex(x => x.UserId).IsUnique();
    }
}
