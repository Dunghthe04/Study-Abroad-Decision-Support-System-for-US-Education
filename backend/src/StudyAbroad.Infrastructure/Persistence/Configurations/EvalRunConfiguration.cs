using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class EvalRunConfiguration : IEntityTypeConfiguration<EvalRun>
{
    public void Configure(EntityTypeBuilder<EvalRun> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.ModelVersion).HasMaxLength(64);
        b.Property(x => x.ResultsJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => x.StartedAt);
    }
}
