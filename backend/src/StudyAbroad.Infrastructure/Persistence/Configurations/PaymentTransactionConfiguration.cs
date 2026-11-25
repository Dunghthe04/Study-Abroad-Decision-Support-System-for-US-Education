using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("payment_transactions", t => t.HasCheckConstraint("ck_payment_transactions_status",
            "status IN ('pending', 'paid', 'mismatch', 'expired', 'cancelled')"));
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);   // [UC-80] xóa user thì giữ giao dịch, bỏ liên kết
        b.HasOne<UserSubscription>().WithMany().HasForeignKey(x => x.UserSubscriptionId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x => x.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.HandledByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<DeletedBuyerRetention>().WithMany().HasForeignKey(x => x.DeletedBuyerRetentionId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.SubscriptionPlanId).HasColumnName("plan_id");
        b.Property(x => x.AmountVnd).HasPrecision(12, 2);
        b.Property(x => x.ReceivedAmountVnd).HasPrecision(12, 2);
        b.Property(x => x.Gateway).HasMaxLength(64).IsRequired();
        b.Property(x => x.GatewayTransactionId).HasMaxLength(128);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("pending");
        b.Property(x => x.PlanKind).HasMaxLength(16).IsRequired();
        b.Property(x => x.PlanName).HasMaxLength(128).IsRequired();
        b.Property(x => x.Description).HasMaxLength(32).IsRequired();
        b.Property(x => x.CheckoutUrl).HasMaxLength(512).IsRequired();
        b.Property(x => x.QrCode).IsRequired();
        b.Property(x => x.AdminNote).HasMaxLength(1000);
        b.HasIndex(x => x.OrderCode).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.DeletedBuyerRetentionId);
        // Mỗi người tối đa 1 đơn đang chờ thanh toán
        b.HasIndex(x => x.UserId, "ux_payment_transactions_one_pending_per_user").IsUnique().HasFilter("status = 'pending'")
            .HasDatabaseName("ux_payment_transactions_one_pending_per_user");
    }
}
