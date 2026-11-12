using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ReviewBatchConfiguration : IEntityTypeConfiguration<ReviewBatch>
{
    public void Configure(EntityTypeBuilder<ReviewBatch> b)
    {
        b.ToTable("review_batches", t => t.HasComment(
            "Đợt duyệt mẫu: một mẫu ngẫu nhiên các câu trả lời của hệ thống để người duyệt chấm đúng sai (đo tỷ lệ lỗi mà hàng đợi không thấy). " +
            "Không chứa nội dung chat; danh sách câu nằm ở review_batch_items."));

        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired().HasComment("Tên đợt do người tạo đặt.");
        b.Property(x => x.CreatedByUserId).HasComment("Admin tạo đợt. Xóa tài khoản thì để trống, đợt vẫn giữ.");
        b.Property(x => x.Seed).HasMaxLength(64).IsRequired()
            .HasComment("Hạt giống của phép bốc ngẫu nhiên: cùng seed, cùng khoảng ngày và cùng dữ liệu thì ra đúng mẫu đó.");
        b.Property(x => x.SizePerStratum).HasComment("Số câu muốn bốc cho mỗi nhóm skill (số thực tế có thể ít hơn nếu nhóm không đủ câu).");
        b.Property(x => x.SampledFromUtc).HasComment("Đầu khoảng thời gian của các câu được xét (tính cả đầu này). Trống là không giới hạn.");
        b.Property(x => x.SampledToUtcExclusive).HasComment("Cuối khoảng thời gian của các câu được xét (không tính đầu này). Trống là không giới hạn.");
        b.HasIndex(x => x.CreatedAt);
    }
}

public class ReviewBatchReviewerConfiguration : IEntityTypeConfiguration<ReviewBatchReviewer>
{
    public void Configure(EntityTypeBuilder<ReviewBatchReviewer> b)
    {
        b.ToTable("review_batch_reviewers", t => t.HasComment(
            "Người được giao duyệt một đợt. Chỉ người trong danh sách mới xem được nội dung các câu của đợt (mỗi lần xem vẫn ghi nhật ký)."));

        b.HasKey(x => x.Id);
        b.HasOne<ReviewBatch>().WithMany().HasForeignKey(x => x.ReviewBatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ReviewBatchId, x.UserId }).IsUnique();
        b.HasIndex(x => x.UserId);
    }
}

public class ReviewBatchItemConfiguration : IEntityTypeConfiguration<ReviewBatchItem>
{
    public void Configure(EntityTypeBuilder<ReviewBatchItem> b)
    {
        b.ToTable("review_batch_items", t => t.HasComment(
            "Một câu trả lời nằm trong mẫu của một đợt. Danh sách được lưu cố định để mẫu không bị bốc lại. " +
            "Xóa tin nhắn gốc (ví dụ học sinh xóa tài khoản) thì dòng này và các kết luận của nó cũng mất."));

        b.HasKey(x => x.Id);
        b.HasOne<ReviewBatch>().WithMany().HasForeignKey(x => x.ReviewBatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.ChatMessageId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.ChatMessageId).HasComment("Tin trả lời của hệ thống được duyệt (bảng chat_messages).");
        b.Property(x => x.Stratum).HasMaxLength(64).IsRequired()
            .HasComment("Nhóm dùng để chia tầng khi bốc mẫu (ví dụ skill của lượt), để nhóm ít gặp vẫn có mặt trong mẫu.");
        b.Property(x => x.Position).HasComment("Thứ tự trong đợt, theo thứ tự bốc.");
        b.HasIndex(x => new { x.ReviewBatchId, x.ChatMessageId }).IsUnique();
        b.HasIndex(x => new { x.ReviewBatchId, x.Position });
        b.HasIndex(x => x.ChatMessageId);
    }
}

public class ReviewVerdictConfiguration : IEntityTypeConfiguration<ReviewVerdict>
{
    public void Configure(EntityTypeBuilder<ReviewVerdict> b)
    {
        static string In(string column, string[] values) => $"{column} IN ({string.Join(", ", values.Select(v => $"'{v}'"))})";

        b.ToTable("review_verdicts", t =>
        {
            t.HasComment(
                "Kết luận của MỘT người duyệt về MỘT câu trong đợt: nhãn ý của SLM đúng không và câu trả lời đúng không. " +
                "Mỗi câu có thể có nhiều người chấm độc lập (một dòng mỗi người). Không chứa nội dung chat.");
            t.HasCheckConstraint("ck_review_verdicts_label_verdict", In("label_verdict", ReviewLabelVerdicts.All));
            t.HasCheckConstraint("ck_review_verdicts_answer_verdict", In("answer_verdict", ReviewAnswerVerdicts.All));
            t.HasCheckConstraint("ck_review_verdicts_cause", $"cause IS NULL OR {In("cause", ReviewCauses.All)}");
            // Nhãn đúng chỉ có nghĩa khi nhãn SLM gán là sai; nguyên nhân chỉ có nghĩa khi câu trả lời sai
            t.HasCheckConstraint("ck_review_verdicts_correct_intent", "correct_intent IS NULL OR label_verdict = 'wrong'");
            t.HasCheckConstraint("ck_review_verdicts_cause_needs_wrong", "cause IS NULL OR answer_verdict = 'wrong'");
        });

        b.HasKey(x => x.Id);
        b.HasOne<ReviewBatchItem>().WithMany().HasForeignKey(x => x.ReviewBatchItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.ReviewBatchItemId).HasComment("Câu được chấm (bảng review_batch_items).");
        b.Property(x => x.ReviewerUserId).HasComment("Người chấm. Xóa tài khoản người chấm thì mất các kết luận của người đó.");
        b.Property(x => x.LabelVerdict).HasMaxLength(16).IsRequired().HasComment("Nhãn ý SLM gán có đúng không: correct hoặc wrong.");
        b.Property(x => x.CorrectIntent).HasMaxLength(32)
            .HasComment("Nhãn đúng khi label_verdict là wrong (một trong sáu loại ý của SLM); trống nếu người duyệt không chắc.");
        b.Property(x => x.AnswerVerdict).HasMaxLength(16).IsRequired()
            .HasComment("Câu trả lời của hệ thống: correct, wrong, missing_data (thiếu dữ liệu) hoặc out_of_scope (ngoài phạm vi).");
        b.Property(x => x.Cause).HasMaxLength(24)
            .HasComment("Vì sao sai, chỉ khi answer_verdict là wrong: misread_intent, missing_knowledge, hallucination hoặc technical.");
        b.HasIndex(x => new { x.ReviewBatchItemId, x.ReviewerUserId }).IsUnique();
        b.HasIndex(x => x.ReviewerUserId);
    }
}
