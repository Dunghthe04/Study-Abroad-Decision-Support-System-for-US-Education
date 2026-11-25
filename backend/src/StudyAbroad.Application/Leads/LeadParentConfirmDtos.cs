namespace StudyAbroad.Application.Leads;

// [UC-116] Phụ huynh xác nhận yêu cầu liên hệ của con dưới 16 qua link mail

/// <summary>Kết quả ghi vào lead khi phụ huynh trả lời hoặc khi hết hạn.</summary>
public record LeadParentDecision(string Status, string? ParentEmail, DateTime? DecidedAt, string? DecisionIp, DateTime UpdatedAt);

public record LeadParentTokenRequest(string Token);

public static class LeadParentConfirmStates
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
}

/// <summary>
/// Nội dung trang /parent-consent/lead. ParentEmail là email của phụ huynh đang mở link;
/// DecidedByYou cho biết người bấm trước có phải chính phụ huynh này không.
/// </summary>
public record LeadParentConfirmInfo(
    string State,
    string StudentName,
    int? StudentAge,
    string CenterName,
    IReadOnlyList<string> SharedFields,
    string? ContactPhone,
    string? Message,
    string ParentEmail,
    DateTime? ExpiresAt,
    DateTime? DecidedAt,
    bool DecidedByYou);

/// <summary>[UC-110 bước 1] Một nhóm dữ liệu trong bản xem trước Tóm tắt hồ sơ gửi phụ huynh (giá trị đã định dạng sẵn).</summary>
public record LeadSharedPreviewGroup(string Title, IReadOnlyList<LeadSharedPreviewItem> Items);

public record LeadSharedPreviewItem(string Label, string Value);
