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
        b.Property(x => x.EducationSystem).HasMaxLength(64);
        b.Property(x => x.CurrentEducationLevel).HasMaxLength(32);
        b.Property(x => x.GraduationYear);
        b.Property(x => x.CurrentGrade).HasMaxLength(64);
        b.Property(x => x.GradeScale).HasMaxLength(8).IsRequired();
        b.Property(x => x.OverallGpa).HasPrecision(5, 2);
        b.Property(x => x.GpaMode).HasMaxLength(16);
        b.Property(x => x.OverallGpa10).HasPrecision(4, 2);
        b.Property(x => x.IntendedMajor).HasMaxLength(128);
        b.Property(x => x.Province).HasMaxLength(64);
        b.Property(x => x.TargetIntake).HasMaxLength(16);
        // điểm thi đọc từ profile_certifications, không có cột trong bảng này
        b.Ignore(x => x.Ielts);
        b.Ignore(x => x.Toefl);
        b.Ignore(x => x.Duolingo);
        b.Ignore(x => x.Sat);
        b.Ignore(x => x.Act);
        b.Property(x => x.EnglishTestPlanned).HasDefaultValue(false);
        b.Property(x => x.StandardizedTestPlanned).HasDefaultValue(false);
        b.Property(x => x.ExtracurricularTier);
        b.ToTable(t => t.HasCheckConstraint("ck_student_profiles_extracurricular_tier",
            "extracurricular_tier IS NULL OR extracurricular_tier BETWEEN 1 AND 5"));
        b.Property(x => x.BudgetTier).HasMaxLength(32);
        b.Property(x => x.BudgetMinUsd).HasPrecision(12, 2);
        b.Property(x => x.BudgetMaxUsd).HasPrecision(12, 2);
        b.Property(x => x.MaxExpectedTuitionUsd).HasPrecision(12, 2);
        b.Property(x => x.FundingSource).HasMaxLength(32);
        b.Property(x => x.ScholarshipNeed).HasMaxLength(16);
        b.Property(x => x.FinancialNotes).HasMaxLength(1000);
        b.Property(x => x.PreferredStates).HasColumnType("text[]");
        b.Property(x => x.TargetSchoolType).HasMaxLength(32).HasDefaultValue("all");
        b.Property(x => x.TranscriptFileUrl).HasMaxLength(512);
        b.Property(x => x.DraftFlowStateJson).HasColumnType("jsonb");
        b.Property(x => x.FlowSubmittedAt);
        b.HasIndex(x => x.UserId).IsUnique();
    }
}
