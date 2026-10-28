using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class RecommendationItemConfiguration : IEntityTypeConfiguration<RecommendationItem>
{
    public void Configure(EntityTypeBuilder<RecommendationItem> b)
    {
        b.ToTable("recommendation_items");
        b.HasKey(x => x.Id);
        b.HasOne<Recommendation>().WithMany().HasForeignKey(x => x.RecommendationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<University>().WithMany().HasForeignKey(x => x.UniversityId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.ProgramName).HasMaxLength(256);
        b.Property(x => x.TierAi).HasMaxLength(32);
        b.Property(x => x.TierRule).HasMaxLength(32);
        b.Property(x => x.MatchStrength).HasPrecision(5, 2);
        b.Property(x => x.RuleCodesTriggered).HasMaxLength(256);
        b.Property(x => x.FinancialFitStatus).HasMaxLength(32).HasDefaultValue("affordable");
        b.HasIndex(x => x.RecommendationId);
        b.HasIndex(x => x.UniversityId);
    }
}
