using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> b)
    {
        b.ToTable("knowledge_documents", t =>
        {
            t.HasComment("Kho tri thức cho chat tư vấn: mỗi dòng là một cặp câu hỏi mẫu và câu trả lời đã chốt, nhập từ file Excel (sheet knowledge_base). Chat so vector của câu hỏi học sinh với vector của question để tìm câu trả lời.");
            var docTypes = string.Join(",", KnowledgeDocTypes.All.Select(x => $"'{x}'"));
            t.HasCheckConstraint("ck_knowledge_documents_doc_type", $"doc_type IN ({docTypes})");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.EntryNo).IsRequired().HasComment("Cột id trong file Excel, khóa để cập nhật khi nhập lại file. Không trùng.");
        b.Property(x => x.DocType).HasMaxLength(32).IsRequired().HasComment("Loại nội dung, một trong 13 loại: faq, glossary, proc_overview, proc_prepare, proc_steps, proc_checklist, proc_mistakes, timeline, compare, situation, data_table, resource, notice.");
        b.Property(x => x.Label).HasMaxLength(64).IsRequired().HasComment("Nhãn chủ đề LABEL_... (ví dụ LABEL_VISA_F1, LABEL_CHI_PHI) để lọc và thống kê.");
        b.Property(x => x.Question).IsRequired().HasComment("Câu hỏi mẫu. Chỉ câu này được tạo vector để tìm kiếm.");
        b.Property(x => x.Answer).IsRequired().HasComment("Câu trả lời chuẩn đã chốt, đưa cho AI làm căn cứ khi trả lời.");
        b.Property(x => x.Source).HasComment("Tên nguồn bằng chữ. Cần ít nhất một trong source và source_url.");
        b.Property(x => x.SourceUrl).HasComment("Link nguồn kiểm chứng (http, https), nhiều link cách nhau bằng dấu chấm phẩy.");
        b.Property(x => x.RetrievedAt).IsRequired().HasComment("Ngày thu thập hoặc xem lại nguồn, để biết thông tin có cũ không.");
        b.Property(x => x.TopicCode).HasMaxLength(64).HasComment("Mã thủ tục (I-20, DS-160...) nối các dòng cùng một thủ tục. Bắt buộc với dòng proc_*.");
        b.Property(x => x.ImageUrl).HasComment("Link ảnh minh họa (ví dụ ảnh mẫu form). Trống nếu không có ảnh.");
        b.Property(x => x.Note).HasComment("Ghi chú nội bộ cho người thu thập. Không đưa cho học sinh. Note bắt đầu bằng CHỜ DUYỆT thì dòng chưa chốt, import bỏ qua.");
        b.Property(x => x.EmbeddingHash).HasMaxLength(64).HasComment("Mã băm sha256 của question lúc tạo vector, chỉ tạo lại vector khi câu hỏi đổi.");
        b.Property(x => x.IngestedAt).HasComment("Lần gần nhất tạo vector cho dòng này.");

        b.HasIndex(x => x.EntryNo).IsUnique();
        b.HasIndex(x => x.DocType);
        b.HasIndex(x => x.Label);
        b.HasIndex(x => x.TopicCode);
    }
}
