namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Một lượt chat ứng viên cho danh sách mẫu, CHƯA có nội dung: chỉ đủ thông tin để quyết định có được cho xem hay không.
/// OwnerDateOfBirth: ngày sinh của chủ phiên (null nếu chưa khai). OwnerConsentVersions: phiên bản chính sách của các lần phụ huynh đồng ý
/// xử lý dữ liệu của chủ phiên (consents, purpose child_data_processing, granted); rỗng nếu không có.
/// </summary>
public record AiQualityCandidate(
    Guid MessageId, DateTime CreatedAt, DateOnly? OwnerDateOfBirth, IReadOnlyList<string?> OwnerConsentVersions);

/// <summary>Một tin trong ngữ cảnh của lượt (các tin ngay trước câu hỏi, cũ trước mới sau).</summary>
public record AiQualitySampleMessage(string Role, string Content);

/// <summary>Một lượt chat có nội dung THÔ, lấy sau khi đã quyết định cho xem. Chưa che: dịch vụ che trước khi trả ra ngoài.</summary>
public record AiQualitySampleRow(
    Guid MessageId, DateTime CreatedAt, string? Outcome, string? ReplyMetaJson, string? Question, string Answer,
    IReadOnlyList<AiQualitySampleMessage> Context, string? OwnerFullName);

/// <summary>
/// Một lượt mẫu đã che thông tin cá nhân, trả cho admin. Không có mã người dùng, email, mã phiên hay mã tin nhắn.
/// Feature là loại ý SLM đầu tiên của lượt (như báo cáo), TurnMs là thời gian cả lượt; null nếu không có.
/// </summary>
public record AiQualitySampleDto(
    DateTime CreatedAt, string Outcome, string? Feature, long? TurnMs, string? Question, string Answer,
    IReadOnlyList<AiQualitySampleMessage> Context);

/// <summary>
/// Một trang mẫu. TotalCount: số lượt ĐƯỢC xem nội dung trong khoảng (để phân trang). HiddenCount: số lượt cùng loại bị ẩn nội dung
/// vì lý do riêng tư (hội thoại của người dưới 16 tuổi chưa có đồng ý phù hợp), để admin biết danh sách không đủ mọi nhóm.
/// </summary>
public record AiQualitySamplePage(IReadOnlyList<AiQualitySampleDto> Items, int Page, int PageSize, int TotalCount, int HiddenCount);
