using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.ReviewBatches;

/// <summary>
/// Yêu cầu tạo đợt duyệt mẫu. SizePerStratum là số câu muốn bốc cho MỖI nhóm skill. Seed bỏ trống thì hệ thống tự sinh (và lưu lại).
/// From, To là ngày UTC, tính cả hai đầu, bỏ trống là không giới hạn. ReviewerIds là các Admin được giao duyệt; bỏ trống thì người tạo đợt tự duyệt.
/// </summary>
public record CreateReviewBatchRequest(
    string? Name, int SizePerStratum, DateOnly? From, DateOnly? To, IReadOnlyList<Guid>? ReviewerIds, string? Seed);

/// <summary>Đợt vừa tạo. Không có nội dung chat. Strata cho biết mỗi nhóm có bao nhiêu câu và chọn được bao nhiêu.</summary>
public record ReviewBatchCreatedDto(
    Guid Id,
    string Name,
    string Seed,
    int SizePerStratum,
    DateTime? SampledFromUtc,
    DateTime? SampledToUtcExclusive,
    int TotalItems,
    IReadOnlyList<StratumCount> Strata,
    IReadOnlyList<Guid> ReviewerIds,
    DateTime CreatedAt);

/// <summary>Một câu trả lời có thể được bốc: mã tin và meta_json của nó (để suy ra nhóm). Không có nội dung chat.</summary>
public record ReviewCandidate(Guid MessageId, string? ReplyMetaJson);

// ===== Danh sách đợt: chỉ số đo, KHÔNG có nội dung chat =====

/// <summary>Tiến độ của một người duyệt trong một đợt: đã chấm bao nhiêu câu (không có kết luận cụ thể của họ).</summary>
public record ReviewerProgressDto(Guid UserId, string FullName, int Graded);

/// <summary>
/// Một đợt trong danh sách của người dùng hiện tại (đợt họ tạo hoặc được giao duyệt). MyGraded là số câu CHÍNH HỌ đã chấm.
/// IsReviewer cho biết họ có được xem nội dung các câu của đợt không: người tạo mà không được giao thì chỉ thấy số đo.
/// </summary>
public record ReviewBatchListItemDto(
    Guid Id,
    string Name,
    string Seed,
    DateTime CreatedAt,
    int SizePerStratum,
    DateTime? SampledFromUtc,
    DateTime? SampledToUtcExclusive,
    int TotalItems,
    IReadOnlyList<StratumCount> Strata,
    bool IsCreator,
    bool IsReviewer,
    int MyGraded,
    IReadOnlyList<ReviewerProgressDto> Reviewers);

/// <summary>Một câu trong danh sách của đợt: chưa có nội dung. GradedByMe chỉ nói CHÍNH người xem đã chấm chưa, không lộ kết luận của người khác.</summary>
public record ReviewItemRowDto(Guid ItemId, int Position, string Stratum, bool GradedByMe);

public record ReviewItemListDto(Guid BatchId, string BatchName, int TotalItems, int MyGraded, IReadOnlyList<ReviewItemRowDto> Items);

// ===== Một câu: CÓ nội dung chat (dữ liệu cá nhân), chỉ trả sau khi đã ghi nhật ký người xem =====

/// <summary>Kết luận của chính người xem (không bao giờ là của người duyệt khác, để chấm độc lập).</summary>
public record ReviewVerdictDto(string LabelVerdict, string? CorrectIntent, string AnswerVerdict, string? Cause, DateTime UpdatedAt);

/// <summary>
/// Một câu để chấm: câu hỏi kèm nhãn SLM gán, câu hệ thống đã trả lời, vài tin trước đó. KHÔNG có mã học sinh hay mã phiên: người duyệt
/// không cần biết đó là ai. MyVerdict null nếu người xem chưa chấm câu này.
/// </summary>
public record ReviewItemDetailDto(
    Guid ItemId,
    int Position,
    string Stratum,
    ChatReviewQuestionDto? Question,
    ChatReviewMessageDto Reply,
    IReadOnlyList<ChatReviewMessageDto> Context,
    ReviewVerdictDto? MyVerdict);

/// <summary>Kết luận gửi lên. Quy tắc kiểm tra nằm ở dịch vụ (và lặp lại bằng ràng buộc kiểm trong database).</summary>
public record SaveReviewVerdictRequest(string? LabelVerdict, string? CorrectIntent, string? AnswerVerdict, string? Cause);

// ===== Dòng thô từ database cho kho dữ liệu =====

public record ReviewerProgressRow(Guid UserId, string FullName, int Graded);

public record ReviewBatchListRow(
    Guid Id,
    string Name,
    string Seed,
    DateTime CreatedAt,
    int SizePerStratum,
    DateTime? SampledFromUtc,
    DateTime? SampledToUtcExclusive,
    string StrataJson,
    int TotalItems,
    bool IsCreator,
    bool IsReviewer,
    IReadOnlyList<ReviewerProgressRow> Reviewers);

public record ReviewItemRow(Guid ItemId, int Position, string Stratum, bool GradedByMe);

public record ReviewItemsRows(Guid BatchId, string BatchName, IReadOnlyList<ReviewItemRow> Items);

public record ReviewItemDetailRows(
    Guid ItemId,
    int Position,
    string Stratum,
    ChatReviewReplyRow Reply,
    ChatReviewQuestionRow? Question,
    IReadOnlyList<ChatReviewContextRow> Context,
    ReviewVerdict? MyVerdict);

// ===== Báo cáo của một đợt: chỉ số tổng hợp, KHÔNG có nội dung chat, không nêu ai chấm thế nào =====

/// <summary>Một kết luận thô của một người cho một câu (kèm nhóm của câu), đủ để tính báo cáo. Không có nội dung chat.</summary>
public record ReviewVerdictRow(
    Guid ItemId, string Stratum, Guid ReviewerUserId, string LabelVerdict, string? CorrectIntent, string AnswerVerdict, string? Cause,
    DateTime CreatedAt);

public record ReviewReportRows(
    Guid BatchId, string BatchName, string Seed, string StrataJson, int TotalItems, int ReviewerCount, IReadOnlyList<ReviewVerdictRow> Verdicts);

/// <summary>
/// Báo cáo một đợt. ItemsGraded là số câu đã có ít nhất một kết luận (TotalItems là số câu của đợt). Mỗi câu chỉ tính một kết luận:
/// kết luận chấm sớm nhất, để câu nhiều người chấm không bị đếm hai lần.
/// </summary>
public record ReviewReportDto(
    Guid BatchId, string BatchName, string Seed, int TotalItems, int ItemsGraded, int ReviewerCount, ReviewReport Report);
