using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class SlmTrainingSampleConfiguration : IEntityTypeConfiguration<SlmTrainingSample>
{
    public void Configure(EntityTypeBuilder<SlmTrainingSample> b)
    {
        b.ToTable("slm_training_samples", t =>
        {
            t.HasComment(
                "Dữ liệu huấn luyện SLM: mỗi dòng là một tin nhắn của học sinh và nhãn intents mà model phải trả về. " +
                "Chỉ dòng approved được xuất ra JSONL để train.");
            t.HasCheckConstraint("ck_slm_training_samples_type",
                "sample_type IN ('collect_profile','profile_review','scenario','knowledge','center_referral','out_of_scope')");
            t.HasCheckConstraint("ck_slm_training_samples_split", "split IN ('train','val','test')");
            t.HasCheckConstraint("ck_slm_training_samples_review_status",
                "review_status IN ('draft','approved','rejected','needs_fix')");
        });

        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.SampleType).HasMaxLength(32).IsRequired()
            .HasComment("Loại mẫu, tương ứng một sheet của file nguồn.");
        b.Property(x => x.Question).IsRequired()
            .HasComment("Đầu vào: tin nhắn của học sinh, giữ nguyên văn.");
        b.Property(x => x.Profile).HasColumnType("jsonb")
            .HasComment("Đầu vào: hồ sơ đã biết. Trống nếu chưa có.");
        b.Property(x => x.PendingQuestion)
            .HasComment("Đầu vào: câu hệ thống vừa hỏi trước tin nhắn này.");
        b.Property(x => x.Intents).HasColumnType("jsonb").IsRequired().HasDefaultValue("[]")
            .HasComment("Đầu ra: danh sách intent gồm type, span, entities.");
        b.Property(x => x.Context).HasColumnType("jsonb")
            .HasComment("Đầu vào: ngữ cảnh lượt trước.");
        b.Property(x => x.Split).HasMaxLength(8).IsRequired().HasDefaultValue(SlmSampleSplits.Train)
            .HasComment("Tập dùng cho model: train, val hoặc test.");
        b.Property(x => x.SourceReview).HasMaxLength(16)
            .HasComment("Nhãn phiên bản rà soát trong file nguồn. Chỉ để tham khảo.");
        b.Property(x => x.ReviewStatus).HasMaxLength(16).IsRequired().HasDefaultValue(SlmReviewStatuses.Draft)
            .HasComment("Trạng thái duyệt: draft, approved, rejected hoặc needs_fix.");
        b.Property(x => x.ReviewNote).HasMaxLength(1000)
            .HasComment("Ghi chú của người duyệt.");
        b.Property(x => x.ReviewedByUserId).HasComment("Người duyệt gần nhất. Xóa tài khoản thì để trống.");
        b.Property(x => x.ReviewedAt).HasComment("Thời điểm duyệt gần nhất.");
        b.Property(x => x.CreatedByUserId).HasComment("Người tạo hoặc import dòng này. Xóa tài khoản thì để trống.");

        b.HasIndex(x => new { x.ReviewStatus, x.SampleType });
        b.HasIndex(x => x.Split);
        b.HasIndex(x => x.CreatedAt);
    }
}
