namespace StudyAbroad.Application.Admin.ChatReview;

/// <summary>
/// Model đã hiểu câu hỏi ra sao, chỉ gồm MÃ CỐ ĐỊNH (độ tin cậy, có dùng dự phòng không, loại các ý). KHÔNG có đoạn chữ hay thực thể
/// học sinh viết (span, entities): đó là nội dung chat nên chỉ xem ở chi tiết, có ghi lại người xem.
/// </summary>
public record ChatReviewModelInfo(
    double? Confidence,
    bool Fallback,
    IReadOnlyList<string> ModelIntentTypes,
    IReadOnlyList<string> IntentTypes,
    string? OutOfScope);

/// <summary>
/// Một câu cần xem trong hàng đợi: tin trả lời của hệ thống (Role assistant) hoặc dòng báo lỗi khi advisor sập (Role system).
/// Không có nội dung chat. QuestionMessageId là tin của học sinh mà dòng này trả lời. Model là thông tin model gán nhãn cho câu hỏi
/// (null nếu lượt không qua SLM, ví dụ advisor sập). FailedSteps là các bước AI hỏng hẳn, Error là tên loại lỗi (không phải thông báo lỗi).
/// </summary>
public record ChatReviewItemDto(
    Guid MessageId,
    Guid SessionId,
    Guid UserId,
    DateTime CreatedAt,
    string Role,
    string Outcome,
    string ReviewStatus,
    Guid? QuestionMessageId,
    ChatReviewModelInfo? Model,
    IReadOnlyList<string> FailedSteps,
    long? TurnMs,
    string? Error);

/// <summary>Số câu theo trạng thái xử lý trong khoảng (null nghĩa là không giới hạn đầu đó), cho các tab của hàng đợi.</summary>
public record ChatReviewSummaryDto(
    DateOnly? From,
    DateOnly? To,
    int Open,
    int Resolved,
    int Ignored,
    int OpenNoAnswer,
    int OpenFailed);

/// <summary>Dòng thô từ database: chưa phân tích meta_json (làm ở dịch vụ, bằng mã C# kiểm soát được).</summary>
public record ChatReviewRow(
    Guid MessageId,
    Guid SessionId,
    Guid UserId,
    DateTime CreatedAt,
    string Role,
    string Outcome,
    string ReviewStatus,
    Guid? QuestionMessageId,
    string? ReplyMetaJson,
    string? QuestionMetaJson);

/// <summary>Số câu của một (trạng thái, kết quả), từ truy vấn gộp.</summary>
public record ChatReviewCountRow(string ReviewStatus, string Outcome, int Count);
