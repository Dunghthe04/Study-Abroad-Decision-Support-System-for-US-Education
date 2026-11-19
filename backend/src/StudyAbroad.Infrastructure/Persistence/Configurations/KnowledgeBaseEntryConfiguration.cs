using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class KnowledgeBaseEntryConfiguration : IEntityTypeConfiguration<KnowledgeBaseEntry>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseEntry> b)
    {
        b.ToTable("knowledge_base_entries", t =>
        {
            t.HasComment(
                "Kho tri thức do Moderator quản lý: câu hỏi, câu trả lời đã kiểm chứng, nhãn, bậc học, nguồn. " +
                "Chỉ dòng approved chưa xóa được chat dùng.");
            t.HasCheckConstraint("ck_knowledge_base_entries_review_status",
                "review_status IN ('draft','pending','approved','rejected','needs_fix')");
            t.HasCheckConstraint("ck_knowledge_base_entries_origin",
                "origin IN ('manual','excel','chat-unanswered')");
            t.HasCheckConstraint("ck_knowledge_base_entries_label", "label IS NULL OR label ~ '^LABEL_[A-Z0-9_]+$'");
            t.HasCheckConstraint("ck_knowledge_base_entries_levels",
                "levels <@ ARRAY['chung','thcs','thpt','dai_hoc','thac_si','tien_si']::text[]");
            // coalesce: NULL <> '' cho ra NULL, mà CHECK coi NULL là đạt. Không có coalesce thì dòng pending thiếu câu trả lời vẫn lọt
            t.HasCheckConstraint("ck_knowledge_base_entries_ready",
                "review_status NOT IN ('pending','approved') OR (coalesce(btrim(answer),'') <> '' AND label IS NOT NULL " +
                "AND coalesce(btrim(source),'') <> '' AND cardinality(levels) > 0)");
        });

        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.EntryNo).UseIdentityByDefaultColumn()
            .HasComment("Mã số ổn định, trùng cột id của file Excel. Thêm tay thì database cấp số tiếp theo.");
        b.Property(x => x.Label).HasMaxLength(64).HasComment("Nhãn chủ đề LABEL_...; nháp có thể trống.");
        b.Property(x => x.Levels).HasColumnType("text[]").IsRequired().HasDefaultValueSql("'{}'")
            .HasComment("Mã bậc học: chung, thcs, thpt, dai_hoc, thac_si, tien_si.");
        b.Property(x => x.Question).HasMaxLength(500).IsRequired().HasComment("Câu hỏi.");
        b.Property(x => x.Answer).HasMaxLength(4000).HasComment("Câu trả lời đã kiểm chứng; nháp có thể trống.");
        b.Property(x => x.Source).HasMaxLength(2000).HasComment("Trích dẫn nguồn, giữ nguyên văn nhiều dòng.");
        b.Property(x => x.Origin).HasMaxLength(32).IsRequired().HasDefaultValue(KnowledgeOrigins.Manual)
            .HasComment("Dòng đến từ đâu: manual, excel hoặc chat-unanswered.");
        b.Property(x => x.ReviewStatus).HasMaxLength(16).IsRequired().HasDefaultValue(KnowledgeReviewStatuses.Draft)
            .HasComment("Trạng thái duyệt: draft, pending, approved, rejected hoặc needs_fix.");
        b.Property(x => x.ReviewNote).HasMaxLength(1000).HasComment("Ghi chú của người duyệt.");
        b.Property(x => x.ReviewedByUserId).HasComment("Người duyệt gần nhất. Xóa tài khoản thì để trống.");
        b.Property(x => x.ReviewedAt).HasComment("Thời điểm duyệt gần nhất.");
        b.Property(x => x.CreatedByUserId).HasComment("Người tạo hoặc import. Trống với câu lấy từ chat.");
        b.Property(x => x.DeletedAt).HasComment("Xóa mềm: khác null thì chỉ hiện trong mục Đã xóa.");
        b.Property(x => x.DeletedByUserId).HasComment("Người xóa.");

        b.HasIndex(x => x.EntryNo).IsUnique();
        b.HasIndex(x => new { x.ReviewStatus, x.Label });
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.CreatedByUserId);
    }
}