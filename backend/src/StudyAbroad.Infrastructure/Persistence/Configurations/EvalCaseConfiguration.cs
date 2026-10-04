using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class EvalCaseConfiguration : IEntityTypeConfiguration<EvalCase>
{
    public void Configure(EntityTypeBuilder<EvalCase> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Question).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ExpectedAnswer).HasMaxLength(4000);
        b.Property(x => x.StudyLevel).HasMaxLength(32);
        b.Property(x => x.Category).HasMaxLength(32);
        b.Property(x => x.ExpectedSourcesJson).HasColumnType("jsonb");
    }
}
