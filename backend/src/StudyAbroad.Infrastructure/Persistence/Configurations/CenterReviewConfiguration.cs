using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class CenterReviewConfiguration : IEntityTypeConfiguration<CenterReview>
{
    public void Configure(EntityTypeBuilder<CenterReview> b)
    {
        b.ToTable("center_reviews", t => t.HasCheckConstraint(
            "ck_center_reviews_rating",
            "rating >= 1 AND rating <= 5"));
        b.HasKey(x => x.Id);
        b.HasOne<StudyCenter>().WithMany().HasForeignKey(x => x.StudyCenterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Comment).HasMaxLength(2000);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("visible");
        b.Property(x => x.CenterReplyContent).HasMaxLength(1000);
        b.Property(x => x.CenterReplyStatus).HasMaxLength(32);
        b.Property(x => x.CenterReplyRemoveReason).HasMaxLength(500);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CenterReplyAuthorUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CenterReplyRemovedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.StudyCenterId);
        // [UC-45] Mỗi yêu cầu liên hệ chỉ một đánh giá (chốt chặn ở DB khi hai lượt gửi trùng nhau)
        b.HasIndex(x => x.LeadId).IsUnique().HasFilter("lead_id IS NOT NULL");
    }
}
