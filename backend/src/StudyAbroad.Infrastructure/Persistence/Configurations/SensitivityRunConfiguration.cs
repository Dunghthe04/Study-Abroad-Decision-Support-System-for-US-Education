using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class SensitivityRunConfiguration : IEntityTypeConfiguration<SensitivityRun>
{
    public void Configure(EntityTypeBuilder<SensitivityRun> b)
    {
        b.ToTable("sensitivity_runs", t =>
        {
            t.HasComment(
                "[UC-85] Mỗi lần admin phân tích độ nhạy tham số của thuật toán gợi ý trường. Chỉ lưu khi chạy xong trọn vẹn. " +
                "Chạy trên bộ hồ sơ giả lập, không có dữ liệu học sinh thật.");
            t.HasCheckConstraint("ck_sensitivity_runs_counts",
                "profile_count > 0 AND school_count >= 0 AND stable_count >= 0 AND watch_count >= 0 AND sensitive_count >= 0 AND duration_ms >= 0");
        });

        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.CreatedByUserId).HasComment("Admin chạy phân tích. Xóa tài khoản thì để trống, lần chạy vẫn giữ.");
        b.Property(x => x.AlgorithmVersion).HasMaxLength(40).IsRequired()
            .HasComment("Phiên bản thuật toán lúc chạy. Hai lần chạy khác phiên bản thì không so trực tiếp được.");
        b.Property(x => x.ProfileSetVersion).HasMaxLength(16).IsRequired().HasComment("Phiên bản bộ hồ sơ giả lập (v1...).");
        b.Property(x => x.ProfileSetSeed).HasComment("Seed đã dùng để sinh bộ hồ sơ.");
        b.Property(x => x.ProfileCount).HasComment("Số hồ sơ giả lập đã chạy.");
        b.Property(x => x.ProfileSetHash).HasMaxLength(64).IsRequired().HasComment("SHA-256 của bộ hồ sơ, để chắc bộ hồ sơ không bị sửa.");
        b.Property(x => x.SchoolCount).HasComment("Số trường đưa vào phân tích (đang hoạt động, bậc đại học).");
        b.Property(x => x.SchoolDataHash).HasMaxLength(64).IsRequired()
            .HasComment("SHA-256 của các số liệu trường mà thuật toán đọc. Khác nhau nghĩa là dữ liệu trường đã đổi giữa hai lần chạy.");
        b.Property(x => x.RequestJson).HasColumnType("jsonb").IsRequired()
            .HasComment("Yêu cầu: các tham số đã chọn, mức nhích (10%/20%), hai ngưỡng nhãn.");
        b.Property(x => x.ParametersJson).HasColumnType("jsonb").IsRequired()
            .HasComment("Giá trị mọi tham số trong bộ đang chạy lúc phân tích (mốc để nhích).");
        b.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired()
            .HasComment("Kết quả từng tham số: giá trị gốc, từng mức nhích, số cặp đổi nhóm, số chỗ top 4 đổi, nhãn.");
        b.Property(x => x.StableCount).HasComment("Số tham số nhãn Ổn định.");
        b.Property(x => x.WatchCount).HasComment("Số tham số nhãn Cần theo dõi.");
        b.Property(x => x.SensitiveCount).HasComment("Số tham số nhãn Nhạy.");
        b.Property(x => x.DurationMs).HasComment("Thời gian chạy (mili giây).");
        b.HasIndex(x => x.CreatedAt);
    }
}
