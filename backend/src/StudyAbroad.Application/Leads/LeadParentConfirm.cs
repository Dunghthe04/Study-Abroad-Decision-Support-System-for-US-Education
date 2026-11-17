using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [UC-116] Token trong link mail hỏi phụ huynh đồng ý gửi yêu cầu liên hệ: "bí mật.mã liên kết".
/// Bí mật chung cho cả lead (DB lưu SHA-256), mã liên kết cho biết phụ huynh nào bấm.
/// </summary>
public static class LeadParentConfirm
{
    /// <summary>Link hết hạn sau 7 ngày, giống thư mời liên kết phụ huynh.</summary>
    public const int ExpiryDays = ParentInvite.ExpiryDays;

    public static string ComposeToken(string secret, Guid linkId) => $"{secret}.{linkId:N}";

    /// <summary>Tách token; trả hash của bí mật để tra DB. False nếu sai định dạng.</summary>
    public static bool TryParseToken(string? token, out string secretHash, out Guid linkId)
    {
        secretHash = string.Empty;
        linkId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return false;

        var parts = token.Trim().Split('.');
        if (parts.Length != 2 || parts[0].Length == 0 || !Guid.TryParseExact(parts[1], "N", out linkId))
            return false;

        secretHash = ParentInvite.HashToken(parts[0]);
        return true;
    }

    /// <summary>Lead còn chờ nhưng đã quá hạn (hết hạn lười: ghi trạng thái khi có người mở link).</summary>
    public static bool IsPastDeadline(Lead lead, DateTime utcNow) =>
        lead.Status == LeadStatuses.PendingParent && lead.ParentTokenExpiresAt <= utcNow;

    /// <summary>Tên nhóm dữ liệu chia sẻ để ghi trong mail cho phụ huynh.</summary>
    public static string FieldLabel(string key) => key switch
    {
        LeadSharedFields.Contact => "Thông tin liên hệ: họ tên, email (và số điện thoại nếu con nhập)",
        LeadSharedFields.Academic => "Học lực: GPA, hệ học, lớp hiện tại",
        LeadSharedFields.Tests => "Điểm thi: tiếng Anh, SAT, ACT",
        LeadSharedFields.Goals => "Mục tiêu: bậc học, ngành dự định",
        LeadSharedFields.Budget => "Mức ngân sách, mức hoạt động ngoại khóa",
        _ => key,
    };
}
