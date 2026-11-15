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

// ===== Chi tiết một câu: CÓ nội dung chat (dữ liệu cá nhân), chỉ trả sau khi đã ghi nhật ký người xem =====

/// <summary>Một ý do model đọc ra, gồm cả đoạn chữ và thực thể học sinh viết (nội dung). Entities là JSON nguyên dạng.</summary>
public record ChatReviewModelIntentDto(string Type, string Span, System.Text.Json.JsonElement? Entities);

/// <summary>Nhãn model gán cho câu hỏi, đầy đủ để admin đối chiếu: độ tin cậy, dự phòng, loại ngoài phạm vi, loại ý đã chạy, và từng ý model đọc ra.</summary>
public record ChatReviewModelDetail(
    double? Confidence,
    bool Fallback,
    string? OutOfScope,
    IReadOnlyList<string> IntentTypes,
    IReadOnlyList<ChatReviewModelIntentDto> ModelIntents);

/// <summary>Một tin nhắn trong chi tiết: vai trò, thời gian, nội dung.</summary>
public record ChatReviewMessageDto(Guid MessageId, string Role, DateTime CreatedAt, string Content);

/// <summary>Câu hỏi của học sinh kèm nhãn model gán cho nó (null nếu lượt không qua SLM, ví dụ advisor sập).</summary>
public record ChatReviewQuestionDto(Guid MessageId, DateTime CreatedAt, string Content, ChatReviewModelDetail? Model);

/// <summary>
/// Chi tiết một câu cần xem: câu hỏi và nhãn model gán, câu hệ thống đã trả lời (hoặc dòng báo lỗi), vài tin trước đó để hiểu câu nối tiếp
/// (Context, theo thứ tự thời gian, chỉ tin của học sinh và trợ lý). Question null khi không có tin hỏi.
/// </summary>
public record ChatReviewDetailDto(
    Guid MessageId,
    Guid SessionId,
    Guid UserId,
    DateTime CreatedAt,
    string Role,
    string Outcome,
    string ReviewStatus,
    IReadOnlyList<string> FailedSteps,
    long? TurnMs,
    string? Error,
    ChatReviewQuestionDto? Question,
    ChatReviewMessageDto Reply,
    IReadOnlyList<ChatReviewMessageDto> Context);

/// <summary>Dòng thô của tin trả lời kèm nội dung và chủ phiên, để dựng chi tiết.</summary>
public record ChatReviewReplyRow(
    Guid MessageId,
    Guid SessionId,
    Guid UserId,
    DateTime CreatedAt,
    string Role,
    string Outcome,
    string ReviewStatus,
    string Content,
    string? MetaJson,
    Guid? QuestionMessageId);

/// <summary>Dòng thô của tin hỏi: nội dung và meta_json (nơi có nhãn model gán).</summary>
public record ChatReviewQuestionRow(Guid MessageId, DateTime CreatedAt, string Content, string? MetaJson);

/// <summary>Tin ngữ cảnh: các tin trước câu hỏi trong cùng phiên.</summary>
public record ChatReviewContextRow(Guid MessageId, string Role, DateTime CreatedAt, string Content);

/// <summary>Mọi thứ cần để dựng chi tiết một câu, lấy một lần từ database.</summary>
public record ChatReviewDetailRows(
    ChatReviewReplyRow Reply, ChatReviewQuestionRow? Question, IReadOnlyList<ChatReviewContextRow> Context);

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

/// <summary>Nội dung yêu cầu đổi trạng thái xử lý của một câu: open, resolved hoặc ignored.</summary>
public record ChatReviewStatusRequest(string? Status);

/// <summary>Kết quả đổi trạng thái: trạng thái mới và trạng thái trước đó (bằng nhau nghĩa là không có gì đổi).</summary>
public record ChatReviewStatusDto(Guid MessageId, string ReviewStatus, string PreviousStatus);

/// <summary>Số câu của một (trạng thái, kết quả), từ truy vấn gộp.</summary>
public record ChatReviewCountRow(string ReviewStatus, string Outcome, int Count);
