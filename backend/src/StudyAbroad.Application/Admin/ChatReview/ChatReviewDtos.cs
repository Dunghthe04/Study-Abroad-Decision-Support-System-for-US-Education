namespace StudyAbroad.Application.Admin.ChatReview;

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
