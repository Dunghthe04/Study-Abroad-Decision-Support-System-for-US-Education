using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Một dòng kho tri thức đã kiểm chứng, trùng cột với sheet knowledge_base của file Excel. Chat tìm kiếm ngữ nghĩa trên bảng này.</summary>
public class KnowledgeDocument : BaseEntity
{
    /// <summary>Cột id của file Excel, khóa để cập nhật khi nhập lại.</summary>
    public int EntryNo { get; set; }

    /// <summary>Một trong KnowledgeDocTypes.All.</summary>
    public string DocType { get; set; } = string.Empty;

    /// <summary>Nhãn chủ đề LABEL_..., một trong KnowledgeLabels.All.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Câu hỏi mẫu chuẩn, dùng để tính embedding phục vụ tìm kiếm ngữ nghĩa.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>Câu trả lời chuẩn đã kiểm chứng (đưa cho LLM làm ngữ cảnh).</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Tên nguồn bằng chữ. Cần ít nhất một trong Source và SourceUrl.</summary>
    public string? Source { get; set; }

    /// <summary>Link nguồn, nhiều link cách nhau bởi dấu ;.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Ngày xem lại nguồn.</summary>
    public DateOnly RetrievedAt { get; set; }

    /// <summary>Mã thủ tục (I-20, DS-160...) nối các dòng cùng một thủ tục; bắt buộc với dòng proc_*.</summary>
    public string? TopicCode { get; set; }

    public string? ImageUrl { get; set; }

    public string? Note { get; set; }

    /// <summary>Mã băm của Question lúc tạo vector, chỉ tạo lại vector khi câu hỏi đổi.</summary>
    public string? EmbeddingHash { get; set; }

    /// <summary>Lần nạp vector gần nhất vào kho tri thức RAG.</summary>
    public DateTime? IngestedAt { get; set; }
}
