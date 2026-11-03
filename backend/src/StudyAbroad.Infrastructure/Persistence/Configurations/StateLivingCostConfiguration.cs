using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class StateLivingCostConfiguration : IEntityTypeConfiguration<StateLivingCost>
{
    public void Configure(EntityTypeBuilder<StateLivingCost> b)
    {
        b.ToTable("state_living_costs");
        b.HasKey(x => x.StateCode);
        b.Property(x => x.StateCode).HasMaxLength(8).HasColumnName("state_code");
        b.Property(x => x.StateName).HasMaxLength(64).IsRequired();
        b.Property(x => x.CostIndex).HasPrecision(6, 2).HasColumnName("index_score").HasDefaultValue(100.0m);
        b.Property(x => x.AvgLivingUsdYearly).HasPrecision(12, 2).HasColumnName("avg_annual_living_usd");
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.SourceUrl).HasMaxLength(512);
    }
}
