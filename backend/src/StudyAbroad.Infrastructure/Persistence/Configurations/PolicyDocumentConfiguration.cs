using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class PolicyDocumentConfiguration : IEntityTypeConfiguration<PolicyDocument>
{
    public void Configure(EntityTypeBuilder<PolicyDocument> b)
    {
        static string In(string column, string[] values) => $"{column} IN ({string.Join(", ", values.Select(v => $"'{v}'"))})";

        b.ToTable("policy_documents", t =>
        {
            t.HasComment(
                "[UC-103] Các phiên bản chính sách và điều khoản (toàn văn mỗi bản). Bản đã công bố không sửa được; muốn đổi thì tạo bản mới. " +
                "Mỗi loại chỉ có một bản đang hiệu lực và tối đa một bản nháp.");
            t.HasCheckConstraint("ck_policy_documents_kind", In("kind", PolicyKinds.All));
            t.HasCheckConstraint("ck_policy_documents_status", In("status", PolicyStatuses.All));
            // Bản nháp chưa có số phiên bản, chưa công bố; bản đã công bố (kể cả đã hết hiệu lực) phải có đủ số phiên bản, thời điểm và tóm tắt thay đổi
            t.HasCheckConstraint("ck_policy_documents_draft_shape",
                "status <> 'draft' OR (version IS NULL AND published_at IS NULL AND retired_at IS NULL)");
            t.HasCheckConstraint("ck_policy_documents_published_shape",
                "status = 'draft' OR (version IS NOT NULL AND published_at IS NOT NULL AND change_summary IS NOT NULL)");
            t.HasCheckConstraint("ck_policy_documents_retired_shape", "(status = 'retired') = (retired_at IS NOT NULL)");
        });

        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<PolicyDocument>().WithMany().HasForeignKey(x => x.BasedOnId).OnDelete(DeleteBehavior.SetNull);

        b.Property(x => x.Kind).HasMaxLength(32).IsRequired()
            .HasComment("Loại: terms_of_use | privacy_policy | child_data_consent | forum_rules.");
        b.Property(x => x.Version).HasMaxLength(16)
            .HasComment("Số phiên bản yyyy-MM (thêm -02, -03 nếu một tháng nhiều bản), đặt lúc công bố. Trống khi còn là nháp.");
        b.Property(x => x.Status).HasMaxLength(16).IsRequired()
            .HasComment("draft (nháp, sửa được) | published (đang hiệu lực, khóa) | retired (hết hiệu lực, giữ để tra cứu).");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired().HasComment("Tiêu đề hiển thị.");
        b.Property(x => x.Content).IsRequired().HasComment("Toàn văn, Markdown đơn giản. Bản đã công bố không sửa.");
        b.Property(x => x.ChangeSummary).HasMaxLength(1000)
            .HasComment("Tóm tắt thay đổi so với bản trước; bắt buộc khi công bố, hiện cho người dùng khi được hỏi đồng ý lại.");
        b.Property(x => x.RequiresReconsent).HasComment("Bản này có bắt người dùng đồng ý lại không (UC-104).");
        b.Property(x => x.BasedOnId).HasComment("Bản được chép ra để tạo nháp này; trống nếu soạn từ đầu.");
        b.Property(x => x.CreatedByUserId).HasComment("Admin tạo bản (nháp). Xóa tài khoản thì để trống.");
        b.Property(x => x.PublishedAt).HasComment("Lúc công bố (bắt đầu hiệu lực).");
        b.Property(x => x.PublishedByUserId).HasComment("Admin công bố. Xóa tài khoản thì để trống.");
        b.Property(x => x.RetiredAt).HasComment("Lúc hết hiệu lực (có bản mới công bố thay).");

        // Số phiên bản không trùng trong cùng một loại
        b.HasIndex(x => new { x.Kind, x.Version }).IsUnique().HasFilter("version IS NOT NULL");
        // Mỗi loại: đúng một bản đang hiệu lực, tối đa một bản nháp (DB chặn luôn, kể cả khi hai admin bấm cùng lúc)
        // (hai chỉ mục cùng cột kind nên phải đặt tên riêng, không thì EF gộp làm một; tên trong DB đặt rõ vì quy tắc snake_case
        // sẽ đổi tên thành ix_policy_documents_kind / kind1)
        b.HasIndex(x => x.Kind, "one_published").IsUnique().HasFilter("status = 'published'")
            .HasDatabaseName("ux_policy_documents_one_published");
        b.HasIndex(x => x.Kind, "one_draft").IsUnique().HasFilter("status = 'draft'")
            .HasDatabaseName("ux_policy_documents_one_draft");
    }
}
