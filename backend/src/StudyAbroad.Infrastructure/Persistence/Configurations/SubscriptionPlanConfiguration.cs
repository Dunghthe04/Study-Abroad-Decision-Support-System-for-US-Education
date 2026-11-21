using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> b)
    {
        b.ToTable("subscription_plans", t =>
        {
            t.HasCheckConstraint("ck_subscription_plans_kind", "kind IN ('free', 'personal', 'family')");
            // Gói trả phí 1..3650 ngày; gói free không có thời hạn
            t.HasCheckConstraint("ck_subscription_plans_duration",
                "(kind = 'free' AND duration_days IS NULL) OR (kind IN ('personal', 'family') AND duration_days >= 1 AND duration_days <= 3650)");
            t.HasCheckConstraint("ck_subscription_plans_quota_period",
                "quota_period_days IS NULL OR (quota_period_days >= 1 AND (duration_days IS NULL OR quota_period_days <= duration_days))");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.PriceVnd).HasPrecision(12, 2).HasDefaultValue(0m);
        b.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        // Đúng một gói free
        b.HasIndex(x => x.Kind, "ux_subscription_plans_single_free").IsUnique().HasFilter("kind = 'free'")
            .HasDatabaseName("ux_subscription_plans_single_free");
    }
}
