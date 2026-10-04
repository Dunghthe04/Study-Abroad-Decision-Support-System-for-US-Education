using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class AnalysisResultConfiguration : IEntityTypeConfiguration<AnalysisResult>
{
    public void Configure(EntityTypeBuilder<AnalysisResult> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        b.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ModelVersion).HasMaxLength(64);
        b.HasIndex(x => new { x.StudentProfileId, x.Kind, x.CreatedAt });
    }
}
