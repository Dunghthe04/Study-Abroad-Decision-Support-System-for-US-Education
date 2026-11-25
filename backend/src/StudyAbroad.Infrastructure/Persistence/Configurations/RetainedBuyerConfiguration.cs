using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

/// <summary>[UC-75 lớp 2] Họ tên, email người mua của tài khoản đã xóa hẳn; chỉ Admin xem, mỗi lần xem ghi nhật ký.</summary>
public class RetainedBuyerConfiguration : IEntityTypeConfiguration<RetainedBuyer>
{
    public void Configure(EntityTypeBuilder<RetainedBuyer> b)
    {
        b.ToTable("retained_buyers");
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).HasMaxLength(256).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.RetainUntil);
    }
}
