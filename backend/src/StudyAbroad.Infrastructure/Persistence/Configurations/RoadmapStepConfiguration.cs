using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class RoadmapStepConfiguration : IEntityTypeConfiguration<RoadmapStep>
{
    public void Configure(EntityTypeBuilder<RoadmapStep> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.StudyLevel).HasMaxLength(32).IsRequired();
        b.Property(x => x.StepKey).HasMaxLength(32).IsRequired();
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.ResourcesJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => new { x.StudyLevel, x.StepKey }).IsUnique();
    }
}
