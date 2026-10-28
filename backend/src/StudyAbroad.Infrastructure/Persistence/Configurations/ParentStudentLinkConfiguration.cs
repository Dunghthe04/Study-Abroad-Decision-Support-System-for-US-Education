using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ParentStudentLinkConfiguration : IEntityTypeConfiguration<ParentStudentLink>
{
    public void Configure(EntityTypeBuilder<ParentStudentLink> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ParentUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Consent>().WithMany().HasForeignKey(x => x.ConsentId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.ParentEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired().HasDefaultValue(ParentLinkStatuses.Pending);
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.Property(x => x.InviteSentCount).HasDefaultValue(0).IsRequired();
        // Không mời trùng một email đang chờ hoặc đang liên kết với cùng học sinh
        b.HasIndex(x => new { x.StudentUserId, x.ParentEmail }).IsUnique()
            .HasFilter("status IN ('pending', 'active')");
        b.HasIndex(x => new { x.ParentUserId, x.Status });
        b.HasIndex(x => x.TokenHash).IsUnique().HasFilter("token_hash IS NOT NULL");
        // Npgsql ánh xạ uint IsRowVersion vào cột hệ thống xmin (không tạo cột mới)
        b.Property(x => x.Version).IsRowVersion();
    }
}
