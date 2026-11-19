using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Một mục kho tri thức: câu hỏi, câu trả lời đã kiểm chứng, nhãn, bậc học và nguồn. Chỉ mục approved chưa xóa được chat dùng.</summary>
public class KnowledgeBaseEntry : BaseEntity
{
    /// <summary>Mã số ổn định (#12), trùng cột id của file Excel. Thêm tay thì database tự cấp số tiếp theo.</summary>
    public int EntryNo { get; set; }

    /// <summary>Nhãn chủ đề LABEL_..., một trong KnowledgeLabels.All. Nháp có thể để trống.</summary>
    public string? Label { get; set; }

    /// <summary>Mã bậc học (chung, thcs, thpt, dai_hoc, thac_si, tien_si). "chung" không đi cùng mã khác.</summary>
    public List<string> Levels { get; set; } = [];

    public string Question { get; set; } = string.Empty;

    public string? Answer { get; set; }

    /// <summary>Trích dẫn nguồn, giữ nguyên văn nhiều dòng (nhà cung cấp, Mã:, Ngày thu:, URL:...).</summary>
    public string? Source { get; set; }

    /// <summary>manual | excel | chat-unanswered.</summary>
    public string Origin { get; set; } = "manual";

    /// <summary>draft | pending | approved | rejected | needs_fix.</summary>
    public string ReviewStatus { get; set; } = "draft";

    public string? ReviewNote { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Null với câu lấy tự động từ chat.</summary>
    public Guid? CreatedByUserId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }
}