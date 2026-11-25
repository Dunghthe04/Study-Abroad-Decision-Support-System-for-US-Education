using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class CenterReviewReportConfiguration : IEntityTypeConfiguration<CenterReviewReport>
{
    public void Configure(EntityTypeBuilder<CenterReviewReport> b)
    {
        b.ToTable("center_review_reports");
        b.HasKey(x => x.Id);
        b.HasOne<CenterReview>().WithMany().HasForeignKey(x => x.CenterReviewId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<StudyCenter>().WithMany().HasForeignKey(x => x.StudyCenterId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.ReporterRole).HasMaxLength(32).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(64).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(1000);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue(CenterReviewReportStatuses.Open);
        b.Property(x => x.ResolutionNote).HasMaxLength(500);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.CenterReviewId);
        // [UC-151 E2] Mỗi người dùng chỉ có tối đa một báo cáo đang mở cho cùng một đánh giá; sau khi đóng có thể báo cáo lại
        b.HasIndex(x => new { x.CenterReviewId, x.ReporterUserId })
            .IsUnique()
            .HasDatabaseName("ux_center_review_reports_one_open_per_user")
            .HasFilter("status = 'open'");
    }
}
