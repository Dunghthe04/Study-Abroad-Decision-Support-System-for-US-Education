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
        // Yêu cầu phải còn lại làm bằng chứng sau khi dữ liệu trung tâm bị xóa, nên không xóa dây chuyền
        b.HasOne<StudyCenter>().WithMany().HasForeignKey(x => x.StudyCenterId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ProcessedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.CenterName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContactEmail).HasMaxLength(256);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("pending");
        b.Property(x => x.AdminNote).HasMaxLength(1000);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.StudyCenterId);
    }
}
