using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("subscription_plans");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.PriceVnd).HasPrecision(12, 2).HasColumnName("price_monthly_vnd").HasDefaultValue(0m);
        b.Property(x => x.DailyAiChatQuota).HasColumnName("ai_chat_daily_quota").HasDefaultValue(10);
        b.HasIndex(x => x.Code).IsUnique();
    }
}
