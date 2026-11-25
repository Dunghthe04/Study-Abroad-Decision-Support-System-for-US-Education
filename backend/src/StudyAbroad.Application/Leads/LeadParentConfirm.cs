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

    private const string Missing = "Chưa cập nhật";

    /// <summary>
    /// [UC-110 bước 1] Bản xem trước Tóm tắt hồ sơ cho mail phụ huynh, dựng từ đúng dữ liệu trung tâm sẽ thấy
    /// (<see cref="CenterLeadMapper.ToCenterDto"/>): chỉ có các nhóm con đã chọn, mỗi nhóm kèm giá trị thật.
    /// </summary>
    public static IReadOnlyList<LeadSharedPreviewGroup> BuildPreview(CenterReceivedLeadDto view, IReadOnlyList<string> sharedFields)
    {
        var s = view.ProfileSummary;
        var groups = new List<LeadSharedPreviewGroup>();
        foreach (var key in LeadSharedFields.All.Where(sharedFields.Contains))
        {
            IReadOnlyList<LeadSharedPreviewItem> items = key switch
            {
                LeadSharedFields.Contact =>
                [
                    new("Họ tên", Text(view.StudentName)),
                    new("Email", Text(view.StudentEmail)),
                    new("Số điện thoại", Text(view.ContactPhone, "Không nhập")),
                ],
                LeadSharedFields.Academic =>
                [
                    new("GPA tích lũy", s?.OverallGpa is { } gpa ? $"{gpa:0.0#} / 4.0" : Missing),
                    new("Hệ học", EducationSystemLabel(s?.EducationSystem)),
                    new("Lớp hiện tại", Text(s?.CurrentGrade)),
                ],
                LeadSharedFields.Tests =>
                [
                    new("Tiếng Anh", s?.EnglishTestType is { } type ? $"{type.ToUpperInvariant()}: {s.EnglishScore?.ToString("0.#") ?? "chưa có điểm"}" : "Chưa có"),
                    new("SAT", s?.SatScore?.ToString("0") ?? "Chưa có"),
                    new("ACT", s?.ActScore?.ToString("0") ?? "Chưa có"),
                ],
                LeadSharedFields.Goals =>
                [
                    new("Bậc học", TargetLevelLabel(s?.TargetLevel)),
                    new("Ngành dự định", Text(s?.IntendedMajor, "Chưa chọn ngành")),
                ],
                LeadSharedFields.Budget =>
                [
                    new("Mức ngân sách", BudgetTierLabel(s?.BudgetTier)),
                    new("Mức hoạt động ngoại khóa", s?.ExtracurricularTier is { } tier ? $"Bậc {tier}" : "Chưa phân tích"),
                ],
                _ => [],
            };
            groups.Add(new LeadSharedPreviewGroup(GroupTitle(key), items));
        }

        if (!string.IsNullOrWhiteSpace(view.Message))
            groups.Add(new LeadSharedPreviewGroup("Lời nhắn gửi trung tâm", [new("Lời nhắn", view.Message.Trim())]));
        return groups;
    }

    private static string GroupTitle(string key) => key switch
    {
        LeadSharedFields.Contact => "Thông tin liên hệ",
        LeadSharedFields.Academic => "Học lực",
        LeadSharedFields.Tests => "Điểm thi",
        LeadSharedFields.Goals => "Mục tiêu",
        LeadSharedFields.Budget => "Ngân sách và ngoại khóa",
        _ => key,
    };

    private static string Text(string? value, string fallback = Missing) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string EducationSystemLabel(string? value) => value switch
    {
        null or "" => Missing,
        EducationSystems.Standard => "Công lập",
        EducationSystems.Specialized => "Trường chuyên / năng khiếu",
        EducationSystems.DualDegree => "Song bằng",
        EducationSystems.International => "Quốc tế",
        EducationSystems.Private => "Tư thục",
        EducationSystems.Other => "Khác",
        _ => value,
    };

    private static string TargetLevelLabel(string? value) => value switch
    {
        null or "" => Missing,
        StudyLevels.Secondary => "THCS/THPT",
        StudyLevels.CommunityCollege => "Cao đẳng cộng đồng",
        StudyLevels.Undergraduate => "Đại học",
        StudyLevels.Master => "Thạc sĩ",
        StudyLevels.Phd => "Tiến sĩ",
        _ => value,
    };

    // Cùng nhãn với màn trung tâm (CenterLeadsListView.formatBudgetTier)
    private static string BudgetTierLabel(string? value) => value switch
    {
        null or "" => "Chưa xác định",
        "tier_under_20k" => "Dưới $20,000 / năm",
        "tier_20k_35k" => "$20,000 - $35,000 / năm",
        "tier_35k_50k" => "$35,000 - $50,000 / năm",
        "tier_50k_70k" => "$50,000 - $70,000 / năm",
        "tier_above_70k" => "Trên $70,000 / năm",
        _ => value,
    };
}
