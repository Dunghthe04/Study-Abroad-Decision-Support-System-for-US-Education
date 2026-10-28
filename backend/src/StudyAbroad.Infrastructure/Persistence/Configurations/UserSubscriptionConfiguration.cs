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
        b.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.SubscriptionPlanId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.SubscriptionPlanId).HasColumnName("plan_id");
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("active");
        b.HasIndex(x => x.UserId);
    }
}
