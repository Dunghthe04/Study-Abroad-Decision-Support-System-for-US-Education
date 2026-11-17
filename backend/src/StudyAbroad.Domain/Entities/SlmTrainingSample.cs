using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Một dòng dữ liệu huấn luyện SLM: tin nhắn của học sinh và nhãn đầu ra (intents) mà model phải trả về. Nguồn là file du_lieu_slm.xlsx.</summary>
public class SlmTrainingSample : BaseEntity
{
    /// <summary>Loại mẫu, tương ứng một sheet của file nguồn: collect_profile, profile_review, scenario, knowledge, center_referral, out_of_scope.</summary>
    public string SampleType { get; set; } = string.Empty;

    /// <summary>Đầu vào: tin nhắn của học sinh, giữ nguyên văn (kể cả lỗi chính tả).</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>Đầu vào: hồ sơ đã biết, chuỗi JSON. Trống nếu chưa có.</summary>
    public string? Profile { get; set; }

    /// <summary>Đầu vào: câu hệ thống vừa hỏi trước tin nhắn này.</summary>
    public string? PendingQuestion { get; set; }

    /// <summary>Đầu ra: danh sách intent theo thứ tự học sinh nói, chuỗi JSON gồm type, span, entities.</summary>
    public string Intents { get; set; } = "[]";

    /// <summary>Đầu vào: ngữ cảnh lượt trước (trường, trường thông tin vừa nhắc), chuỗi JSON.</summary>
    public string? Context { get; set; }

    /// <summary>Tập dùng cho model: train, val hoặc test.</summary>
    public string Split { get; set; } = "train";

    /// <summary>Nhãn phiên bản rà soát ghi trong file nguồn (v9, v10, v11...). Chỉ để tham khảo, không điều khiển luồng duyệt.</summary>
    public string? SourceReview { get; set; }

    /// <summary>Trạng thái duyệt: draft | pending | approved | rejected | needs_fix. Chỉ dòng approved được xuất.</summary>
    public string ReviewStatus { get; set; } = "draft";

    /// <summary>Ghi chú của người duyệt, ví dụ lý do từ chối hoặc chỗ nhãn cần sửa.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Người duyệt gần nhất.</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>Thời điểm duyệt gần nhất.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Người tạo hoặc import dòng này. Null với câu lấy tự động từ chat.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Câu trả lời mẫu (văn bản tự do). Dòng cũ chỉ có intents thì để trống.</summary>
    public string? Answer { get; set; }

    public string? Topic { get; set; }

    /// <summary>Nguồn của mẫu: manual, excel, chat-unanswered hoặc nguồn tham khảo do người tạo ghi.</summary>
    public string Source { get; set; } = "manual";

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }

    /// <summary>Lần xuất gần nhất (UC-102).</summary>
    public DateTime? ExportedAt { get; set; }
}
