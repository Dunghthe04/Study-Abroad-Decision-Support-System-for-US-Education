using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class CenterDeletionRequestConfiguration : IEntityTypeConfiguration<CenterDeletionRequest>
{
    public void Configure(EntityTypeBuilder<CenterDeletionRequest> b)
    {
        b.ToTable("center_deletion_requests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired().HasDefaultValue("pending");
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.StudyCenterId);
        // Bản ghi yêu cầu xóa phải còn sau khi trung tâm bị xóa (UC-93 cần lưu vết), nên không cascade.
        b.HasOne<StudyCenter>().WithMany().HasForeignKey(x => x.StudyCenterId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.DecisionNote).HasMaxLength(1000);
        b.HasIndex(x => new { x.Status, x.ScheduledPurgeAt });
        // [UC-93] Tài khoản trung tâm bị xóa hẳn thì bỏ liên kết người gửi, giữ bản ghi yêu cầu
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.HandledByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
