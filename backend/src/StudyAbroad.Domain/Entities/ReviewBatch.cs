using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// Một đợt duyệt mẫu: danh sách câu trả lời của hệ thống (đã trả lời được) được bốc ngẫu nhiên để người duyệt đọc và chấm đúng sai.
/// Danh sách câu được lưu cố định (ReviewBatchItem) nên mẫu không bị bốc lại; seed và khoảng ngày được ghi để người khác lặp lại được.
/// </summary>
public class ReviewBatch : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    /// <summary>Hạt giống của phép bốc ngẫu nhiên: cùng seed, cùng khoảng ngày và cùng dữ liệu thì ra đúng mẫu đó.</summary>
    public string Seed { get; set; } = string.Empty;
    /// <summary>Số câu muốn bốc cho mỗi nhóm skill; số câu thực tế có thể ít hơn nếu nhóm không đủ câu.</summary>
    public int SizePerStratum { get; set; }
    /// <summary>Khoảng thời gian của các câu được xét, [từ, đến). Null là không giới hạn đầu đó.</summary>
    public DateTime? SampledFromUtc { get; set; }
    public DateTime? SampledToUtcExclusive { get; set; }
}

/// <summary>Người được giao duyệt một đợt. Chỉ người trong danh sách này mới xem được nội dung các câu của đợt.</summary>
public class ReviewBatchReviewer : BaseEntity
{
    public Guid ReviewBatchId { get; set; }
    public Guid UserId { get; set; }
}

/// <summary>Một câu trả lời nằm trong mẫu của một đợt.</summary>
public class ReviewBatchItem : BaseEntity
{
    public Guid ReviewBatchId { get; set; }
    /// <summary>Tin trả lời của hệ thống được duyệt (bảng chat_messages).</summary>
    public Guid ChatMessageId { get; set; }
    /// <summary>Nhóm dùng để chia tầng khi bốc mẫu (ví dụ skill của lượt), để nhóm ít gặp vẫn có mặt trong mẫu.</summary>
    public string Stratum { get; set; } = string.Empty;
    /// <summary>Thứ tự trong đợt, theo thứ tự bốc.</summary>
    public int Position { get; set; }
}

/// <summary>
/// Kết luận của MỘT người duyệt về MỘT câu trong đợt: nhãn ý của SLM đúng không, và câu trả lời đúng không.
/// Mỗi câu có thể có nhiều người chấm độc lập (một dòng mỗi người).
/// </summary>
public class ReviewVerdict : BaseEntity
{
    public Guid ReviewBatchItemId { get; set; }
    public Guid ReviewerUserId { get; set; }
    /// <summary>ReviewLabelVerdicts: correct hoặc wrong.</summary>
    public string LabelVerdict { get; set; } = string.Empty;
    /// <summary>Nhãn đúng (một trong SlmIntentTypes) khi LabelVerdict là wrong; có thể để trống nếu người duyệt không chắc.</summary>
    public string? CorrectIntent { get; set; }
    /// <summary>ReviewAnswerVerdicts: correct, wrong, missing_data hoặc out_of_scope.</summary>
    public string AnswerVerdict { get; set; } = string.Empty;
    /// <summary>ReviewCauses, chỉ khi AnswerVerdict là wrong.</summary>
    public string? Cause { get; set; }
}
