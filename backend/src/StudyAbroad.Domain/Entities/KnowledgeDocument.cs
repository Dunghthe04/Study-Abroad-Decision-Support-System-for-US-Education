using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Kiến thức hỏi đáp cho AI. Advisor tạo vector cho Question và tìm kiếm trên bảng này; chỉ dòng verified được dùng để trả lời.</summary>
public class KnowledgeDocument : BaseEntity
{
    /// <summary>Mã cố định KB-001..., khóa để cập nhật khi nạp lại dữ liệu</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Câu hỏi mẫu, chữ được embedding để tìm kiếm</summary>
    public string Question { get; set; } = string.Empty;
    /// <summary>Câu trả lời chuẩn, đưa cho AI để viết câu trả lời</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>Nguồn kiểm chứng gốc, dùng làm trích dẫn</summary>
    public string? Quote { get; set; }
    public string? SourceUrl { get; set; }
    /// <summary>official | center | internal</summary>
    public string SourceType { get; set; } = "official";
    /// <summary>Nguồn thu thập, ví dụ "Edu4life, RAW-ON-003"</summary>
    public string? SourceNote { get; set; }
    /// <summary>Chủ đề, dùng để thống kê và lọc</summary>
    public string? Topic { get; set; }
    public string StudyLevel { get; set; } = "general";
    /// <summary>system_advisory (B5 dùng) | center_info (dành cho B6)</summary>
    public string Scope { get; set; } = "system_advisory";
    /// <summary>draft | verified | rejected</summary>
    public string Status { get; set; } = "draft";
    public DateOnly? RetrievedAt { get; set; }
    /// <summary>Hạn kiểm tra lại (phí, quy định hay đổi)</summary>
    public DateTime? ReviewDueAt { get; set; }
    /// <summary>Mã băm của Question lúc tạo vector, chỉ tạo lại vector khi câu hỏi đổi</summary>
    public string? EmbeddingHash { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
