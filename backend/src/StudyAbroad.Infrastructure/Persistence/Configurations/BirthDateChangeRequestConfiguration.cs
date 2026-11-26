using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class BirthDateChangeRequestConfiguration : IEntityTypeConfiguration<BirthDateChangeRequest>
{
    public void Configure(EntityTypeBuilder<BirthDateChangeRequest> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.EvidenceType).HasMaxLength(32);
        b.Property(x => x.EvidencePath).HasMaxLength(1024);
        b.Property(x => x.EvidenceFileName).HasMaxLength(256);
        b.Property(x => x.EvidenceContentType).HasMaxLength(128);
        b.Property(x => x.RepresentativeEmail).HasMaxLength(256);
        b.Property(x => x.RejectReason).HasMaxLength(500);

        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        // [UC-107 E5] Mỗi tài khoản tối đa một yêu cầu Chờ duyệt, kể cả khi bấm gửi hai lần cùng lúc
        b.HasIndex(x => x.UserId, "ux_birth_date_change_requests_one_pending_per_user")
            .IsUnique()
            .HasFilter($"status = '{BirthDateChangeStatuses.Pending}'");
    }
}
