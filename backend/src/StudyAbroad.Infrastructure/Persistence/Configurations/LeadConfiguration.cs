using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> b)
    {
        b.HasKey(x => x.Id);
        // [UC-75 lớp 1b] Xóa hẳn người gửi: giữ yêu cầu tới hết hạn giữ tin (job dọn sau), không xóa theo
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<StudyCenter>().WithMany().HasForeignKey(x => x.StudyCenterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Consent>().WithMany().HasForeignKey(x => x.ConsentId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.StudyLevel).HasMaxLength(32);
        b.Property(x => x.ContactPhone).HasMaxLength(32);
        b.Property(x => x.Message).HasMaxLength(2000);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.SharedFields).HasColumnType("text[]");
        b.HasIndex(x => new { x.StudyCenterId, x.Status });
        // UC-42: đếm số lần gửi trong 7 ngày / trong ngày
        b.HasIndex(x => new { x.SubmittedByUserId, x.CreatedAt });
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        // [UC-116] Phụ huynh xác nhận qua link mail
        b.Property(x => x.ParentTokenHash).HasMaxLength(128);
        b.HasIndex(x => x.ParentTokenHash);
        b.Property(x => x.ParentEmail).HasMaxLength(256);
        b.Property(x => x.ParentDecisionIp).HasMaxLength(64);
        // [UC-150 4a / UC-110 3a] Giữ chờ phụ huynh khi sửa ngày sinh; ActiveSince chỉ tính trong C#
        b.Property(x => x.HeldFromStatus).HasMaxLength(16);
        b.Ignore(x => x.ActiveSince);
    }
}
