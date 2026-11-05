using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ExpertRuleConfiguration : IEntityTypeConfiguration<ExpertRule>
{
    public void Configure(EntityTypeBuilder<ExpertRule> b)
    {
        b.ToTable("expert_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.RuleCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.RuleName).HasMaxLength(256).IsRequired();
        b.Property(x => x.DegreeLevel).HasMaxLength(64).IsRequired().HasDefaultValue("all");
        b.Property(x => x.TargetDirection).HasMaxLength(128);
        b.Property(x => x.ConditionJson).HasColumnType("jsonb");
        b.Property(x => x.SourceReference).HasMaxLength(256);
        b.Property(x => x.CertaintyFactor).HasPrecision(3, 2).HasDefaultValue(0.90m);
        b.Property(x => x.Status).HasMaxLength(32).HasDefaultValue("active");
        b.Property(x => x.VerifiedBy).HasMaxLength(128).HasDefaultValue("USAS Advisory Board");
        b.HasIndex(x => x.RuleCode).IsUnique();
        b.HasIndex(x => x.DegreeLevel);
    }
}
