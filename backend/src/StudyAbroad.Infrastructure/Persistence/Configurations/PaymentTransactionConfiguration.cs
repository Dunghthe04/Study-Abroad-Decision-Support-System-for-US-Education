using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("payment_transactions");
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<UserSubscription>().WithMany().HasForeignKey(x => x.UserSubscriptionId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.AmountVnd).HasPrecision(12, 2);
        b.Property(x => x.Gateway).HasMaxLength(64).IsRequired();
        b.Property(x => x.GatewayTransactionId).HasMaxLength(128);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("pending");
        b.HasIndex(x => x.UserId);
    }
}
