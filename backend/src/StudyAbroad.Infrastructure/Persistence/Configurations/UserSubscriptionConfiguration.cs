using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> b)
    {
        b.ToTable("user_subscriptions");
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // Gói đã có kỳ thì không xóa được (admin chỉ ngừng bán)
        b.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyGroup>().WithMany().HasForeignKey(x => x.FamilyGroupId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.SubscriptionPlanId).HasColumnName("plan_id");
        b.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.PlanName).HasMaxLength(128).IsRequired();
        b.Property(x => x.PriceVnd).HasPrecision(12, 2);
        b.Property(x => x.EndedReason).HasMaxLength(32);
        // Mỗi làn (người, loại) tối đa 1 kỳ active và 1 kỳ queued: hai lần kích hoạt cùng lúc chỉ một bên thắng
        b.HasIndex(x => new { x.UserId, x.Kind }, "ux_user_subscriptions_one_active_per_lane").IsUnique().HasFilter("status = 'active'");
        b.HasIndex(x => new { x.UserId, x.Kind }, "ux_user_subscriptions_one_queued_per_lane").IsUnique().HasFilter("status = 'queued'");
        b.HasIndex(x => new { x.UserId, x.Kind, x.Status });
    }
}
