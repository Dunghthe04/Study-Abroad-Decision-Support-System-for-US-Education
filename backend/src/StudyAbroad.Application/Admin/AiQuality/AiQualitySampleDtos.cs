namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Một lượt chat ứng viên cho danh sách mẫu, CHƯA có nội dung: chỉ đủ thông tin để quyết định có được cho xem hay không.
/// OwnerDateOfBirth: ngày sinh của chủ phiên (null nếu chưa khai). OwnerConsentVersions: phiên bản chính sách của các lần phụ huynh đồng ý
/// xử lý dữ liệu của chủ phiên (consents, purpose child_data_processing, granted); rỗng nếu không có.
/// OwnerIsParentOperated: tài khoản của con do phụ huynh vận hành (users.status = managed): dự án không áp luật dưới 16 cho loại này
/// vì người thao tác là phụ huynh (xem AgeAccess.IsOperatedByParent).
/// SessionDeleted: học sinh đã xóa cuộc trò chuyện này khỏi lịch sử (chat_sessions.deleted_at).
/// </summary>
public record AiQualityCandidate(
    Guid MessageId, DateTime CreatedAt, DateOnly? OwnerDateOfBirth, IReadOnlyList<string?> OwnerConsentVersions,
    bool OwnerIsParentOperated = false, bool SessionDeleted = false);

/// <summary>Vì sao một lượt bị ẩn nội dung: ghi rõ để admin hiểu, không gộp thành "lý do riêng tư" chung chung.</summary>
public enum AiQualityHiddenReason
{
    /// <summary>Học sinh đã xóa cuộc trò chuyện khỏi lịch sử: số liệu vẫn tính, nội dung không cho đọc lại.</summary>
    DeletedByUser,

    /// <summary>Người dưới 16 tuổi mà phụ huynh chưa đồng ý bản chính sách có nêu mục đích đánh giá chất lượng.</summary>
    MinorWithoutConsent,
}

/// <summary>Giá trị HiddenReason trả cho admin (chuỗi cố định, giao diện dựa vào đó để ghi nhãn).</summary>
public static class AiQualityHiddenReasons
{
    public const string DeletedByUser = "deleted_by_user";
    public const string MinorWithoutConsent = "minor_without_consent";

    public static string Code(AiQualityHiddenReason reason) => reason switch
    {
        AiQualityHiddenReason.DeletedByUser => DeletedByUser,
        AiQualityHiddenReason.MinorWithoutConsent => MinorWithoutConsent,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };
}

/// <summary>
/// Thông tin của một lượt bị ẩn nội dung, đủ để hiện dòng giữ chỗ: KHÔNG có chữ nào của học sinh hay của trợ lý.
/// ReplyMetaJson là meta_json của tin trả lời (chỉ có số liệu: thời gian, loại ý, bước hỏng, token). SlmFallback tính sẵn trong database từ
/// meta của tin hỏi, vì meta tin hỏi có đoạn chữ học sinh viết nên không được tải lên.
/// </summary>
public record AiQualityHiddenRow(Guid MessageId, DateTime CreatedAt, string? Outcome, string? ReplyMetaJson, bool SlmFallback);

/// <summary>Một tin trong ngữ cảnh của lượt (các tin ngay trước câu hỏi, cũ trước mới sau).</summary>
public record AiQualitySampleMessage(string Role, string Content);

/// <summary>Một lượt chat có nội dung THÔ, lấy sau khi đã quyết định cho xem. Chưa che: dịch vụ che trước khi trả ra ngoài.</summary>
public record AiQualitySampleRow(
    Guid MessageId, DateTime CreatedAt, string? Outcome, string? ReplyMetaJson, string? Question, string Answer,
    IReadOnlyList<AiQualitySampleMessage> Context, string? OwnerFullName, string? QuestionMetaJson = null);

/// <summary>
/// Một lượt mẫu đã che thông tin cá nhân, trả cho admin. Không có mã người dùng, email, mã phiên hay mã tin nhắn.
/// Feature là loại ý SLM đầu tiên của lượt (như báo cáo), TurnMs là thời gian cả lượt; null nếu không có.
/// HiddenReason khác null là DÒNG GIỮ CHỖ của lượt bị ẩn nội dung (AiQualityHiddenReasons): vẫn đúng vị trí thời gian, có thời điểm, loại kết quả,
/// tính năng, thời gian phản hồi, nhưng Question và Answer là null, Context rỗng.
/// </summary>
public record AiQualitySampleDto(
    DateTime CreatedAt, string Outcome, string? Feature, long? TurnMs, string? Question, string? Answer,
    IReadOnlyList<AiQualitySampleMessage> Context, string? HiddenReason = null);

/// <summary>
/// Một trang mẫu. TotalCount: MỌI lượt cùng loại trong khoảng, kể cả lượt bị ẩn (hiện thành dòng giữ chỗ), nên khớp số trên thẻ của báo cáo.
/// HiddenCount: số lượt bị ẩn nội dung trong khoảng, tách theo lý do: HiddenDeletedCount (học sinh đã xóa cuộc trò chuyện) và HiddenMinorCount
/// (người dưới 16 tuổi chưa có đồng ý phù hợp).
/// </summary>
public record AiQualitySamplePage(
    IReadOnlyList<AiQualitySampleDto> Items, int Page, int PageSize, int TotalCount, int HiddenCount,
    int HiddenDeletedCount = 0, int HiddenMinorCount = 0);
