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
                "Mọi mẫu chưa xóa được xuất ra JSONL/Excel để train (UC-96).");
            t.HasCheckConstraint("ck_slm_training_samples_type",
                "sample_type IN ('collect_profile','profile_review','scenario','knowledge','center_referral','out_of_scope')");
            t.HasCheckConstraint("ck_slm_training_samples_split", "split IN ('train','val','test')");
        });

        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.SetNull);

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
        b.Property(x => x.Version).IsRequired().HasDefaultValue(1)
            .HasComment("Phiên bản hiện tại của mẫu.");
        b.Property(x => x.CreatedByUserId).HasComment("Người tạo hoặc import dòng này. Xóa tài khoản thì để trống.");

        b.Property(x => x.Source).HasMaxLength(256).IsRequired().HasDefaultValue(SlmSampleSources.Manual)
            .HasComment("Nguồn: manual, excel hoặc nguồn tham khảo.");
        b.Property(x => x.DeletedAt).HasComment("Xóa mềm: khác null thì không hiện ở Đang dùng và không xuất.");
        b.Property(x => x.DeletedByUserId).HasComment("Người xóa.");
        b.Property(x => x.ExportedAt).HasComment("Lần xuất gần nhất (UC-96).");

        b.HasIndex(x => x.SampleType);
        b.HasIndex(x => x.DeletedAt);
        b.HasIndex(x => x.CreatedByUserId);
        b.HasIndex(x => x.CreatedAt);
    }
}
