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
            // Gói trả phí chỉ 30 hoặc 90 ngày; gói free không có thời hạn (làm mới đầu tháng)
            t.HasCheckConstraint("ck_subscription_plans_duration", "kind = 'free' OR duration_days IN (30, 90)");
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
