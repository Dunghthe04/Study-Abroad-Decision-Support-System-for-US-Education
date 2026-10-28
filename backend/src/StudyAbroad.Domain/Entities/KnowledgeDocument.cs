using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Kiến thức đã kiểm chứng cho AI: mỗi dòng Đã kiểm chứng ở sheet 05 / 88 cặp Q&A là một bản ghi, script nạp sẽ embed sang kho tri thức phục vụ B5 RAG (UC-51, 52, 77).</summary>
public class KnowledgeDocument : BaseEntity
{
    /// <summary>Tiêu đề hoặc phần đầu của câu hỏi.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Câu hỏi mẫu chuẩn, dùng để tính embedding phục vụ tìm kiếm ngữ nghĩa B5.</summary>
    public string? Question { get; set; }

    /// <summary>Câu trả lời chuẩn đã kiểm chứng (đưa cho LLM làm ngữ cảnh context).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Nhãn chủ đề (từ cột "Nhãn phân loại", ví dụ: Chi phí, Visa, Hồ sơ...).</summary>
    public string? Topic { get; set; }

    /// <summary>Bậc học: secondary, community_college, undergraduate, master, phd, general.</summary>
    public string StudyLevel { get; set; } = "general";

    /// <summary>Giai đoạn quy trình (tùy chọn: B1, B2, B3, B4, B5, B6...).</summary>
    public string? Stage { get; set; }

    /// <summary>Loại tài liệu: faq, policy, guide, requirement...</summary>
    public string DocType { get; set; } = "faq";

    /// <summary>Phạm vi: system_advisory (cho B5) hoặc center_info (cho B6).</summary>
    public string Scope { get; set; } = "system_advisory";

    /// <summary>Trạng thái kiểm duyệt: draft | verified | rejected.</summary>
    public string Status { get; set; } = "draft";

    /// <summary>Nguồn kiểm chứng gốc (ví dụ: "EducationUSA - U.S. Department of State").</summary>
    public string? Quote { get; set; }

    /// <summary>Loại nguồn: official | center.</summary>
    public string SourceType { get; set; } = "official";

    /// <summary>Nguồn tư vấn thực tế và mã nhận diện (ví dụ: "Edu4life, RAW-ON-003").</summary>
    public string? SourceNote { get; set; }

    /// <summary>Đường link URL tài liệu kiểm chứng.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Ngày thu thập thông tin.</summary>
    public DateOnly? RetrievedAt { get; set; }

    /// <summary>Hạn rà soát lại thông tin (phí, quy định visa hay thay đổi).</summary>
    public DateTime? ReviewDueAt { get; set; }

    /// <summary>Phiên bản tài liệu (mặc định 1).</summary>
    public int Version { get; set; } = 1;

    /// <summary>Vector 1024 chiều (mô hình BGE-M3) của trường Question.</summary>
    public float[]? Embedding { get; set; }

    /// <summary>ID người kiểm duyệt (Moderator / Admin).</summary>
    public Guid? VerifiedByUserId { get; set; }

    /// <summary>Thời điểm kiểm duyệt.</summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>Lần nạp vector gần nhất vào kho tri thức RAG.</summary>
    public DateTime? IngestedAt { get; set; }
}
