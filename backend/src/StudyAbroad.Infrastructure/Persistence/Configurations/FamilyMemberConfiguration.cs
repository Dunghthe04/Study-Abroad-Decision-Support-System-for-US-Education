using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
{
    public void Configure(EntityTypeBuilder<FamilyMember> b)
    {
        b.ToTable("family_members");
        b.HasKey(x => x.Id);
        b.HasOne<FamilyGroup>().WithMany().HasForeignKey(x => x.FamilyGroupId).OnDelete(DeleteBehavior.Cascade);
        // Thành viên bị xóa hẳn thì giữ dòng (đã kết thúc), chỉ bỏ liên kết
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.InvitedEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.SeatRole).HasMaxLength(16).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.Property(x => x.EndedReason).HasMaxLength(32);
        b.Property(x => x.InviteSentCount).HasDefaultValue(0).IsRequired();
        // Npgsql ánh xạ uint IsRowVersion vào cột hệ thống xmin (không tạo cột mới)
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => x.TokenHash).IsUnique().HasFilter("token_hash IS NOT NULL");
        // Một người chỉ ở 1 nhóm đang hoạt động; một email chỉ có 1 lời mời đang chờ trong một nhóm
        b.HasIndex(x => x.UserId, "ux_family_members_one_active_per_user").IsUnique().HasFilter("status = 'active'");
        b.HasIndex(x => new { x.FamilyGroupId, x.InvitedEmail }, "ux_family_members_one_pending_per_email").IsUnique().HasFilter("status = 'pending'");
        b.HasIndex(x => new { x.FamilyGroupId, x.Status });
    }
}
