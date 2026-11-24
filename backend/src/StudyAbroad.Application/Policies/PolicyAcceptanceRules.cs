using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Policies;

/// <summary>Một phiên bản đã công bố của một loại tài liệu (đang hiệu lực hoặc đã hết hiệu lực), chỉ gồm phần quy tắc cần.</summary>
public sealed record PublishedPolicyVersion(string Version, bool RequiresReconsent);

/// <summary>Tình trạng đồng ý của một người dùng với MỘT loại tài liệu.</summary>
public enum PolicyAcceptanceState
{
    /// <summary>Loại này chưa có bản nào được công bố: không có gì để đồng ý, không chặn ai.</summary>
    NotPublished,
    /// <summary>Đã đồng ý đúng bản đang hiệu lực.</summary>
    Accepted,
    /// <summary>Có bản mới hơn nhưng không bắt đồng ý lại (sửa chính tả...): chỉ báo, không chặn.</summary>
    UpdateOptional,
    /// <summary>Có bản mới hơn và ít nhất một bản trong số đó bắt đồng ý lại: phải đồng ý mới dùng tiếp.</summary>
    UpdateRequired,
    /// <summary>Chưa từng đồng ý (hoặc đã từ chối, hoặc bản ghi cũ không khớp bản nào đã công bố): phải đồng ý.</summary>
    NeverAccepted,
}

/// <summary>
/// [UC-104] Quy tắc "người dùng có phải đồng ý (lại) không". Hàm thuần, không đụng DB, để kiểm thử đầy đủ mọi tình huống.
/// Điểm mấu chốt: KHÔNG chỉ nhìn bản mới nhất. Người dùng đồng ý 2026-11, sau đó có 2026-11-02 (bắt buộc) rồi 2026-11-03
/// (không bắt buộc) thì vẫn phải đồng ý, vì bản bắt buộc ở giữa chưa từng được họ đồng ý.
/// </summary>
public static class PolicyAcceptanceRules
{
    /// <summary>Các loại tài liệu mỗi tài khoản phải đồng ý. Đồng ý của phụ huynh cho con (UC-115) và nội quy diễn đàn có luồng riêng.</summary>
    public static readonly string[] AccountKinds = [PolicyKinds.TermsOfUse, PolicyKinds.PrivacyPolicy];

    /// <param name="published">Mọi phiên bản đã công bố của loại này (kể cả bản đã hết hiệu lực). Bản nháp không đưa vào.</param>
    /// <param name="acceptedVersion">Phiên bản trong lần đồng ý GẦN NHẤT của người dùng; null nếu chưa từng đồng ý hoặc lần gần nhất là từ chối.</param>
    public static PolicyAcceptanceState Evaluate(IReadOnlyCollection<PublishedPolicyVersion> published, string? acceptedVersion)
    {
        if (published.Count == 0)
            return PolicyAcceptanceState.NotPublished;

        // Số phiên bản dạng yyyy-MM, -02, -03: so theo thứ tự chữ là đúng thứ tự thời gian (cùng cách UC-103 và UC-90 dùng)
        var current = published.Select(v => v.Version).Max(StringComparer.Ordinal)!;

        // Phiên bản ghi lại không thuộc bản nào đã công bố (dữ liệu lạ, "v1.0" cũ...) thì không tin: coi như chưa đồng ý.
        // Nếu không, một chuỗi như "v1.0" (chữ v đứng sau chữ số) sẽ bị tính nhầm là "mới hơn mọi bản".
        if (acceptedVersion is null || !published.Any(v => v.Version == acceptedVersion))
            return PolicyAcceptanceState.NeverAccepted;

        if (string.CompareOrdinal(acceptedVersion, current) >= 0)
            return PolicyAcceptanceState.Accepted;

        var newer = published.Where(v => string.CompareOrdinal(v.Version, acceptedVersion) > 0);
        return newer.Any(v => v.RequiresReconsent) ? PolicyAcceptanceState.UpdateRequired : PolicyAcceptanceState.UpdateOptional;
    }

    /// <summary>Tình trạng này có chặn người dùng dùng tiếp hệ thống không.</summary>
    public static bool Blocks(PolicyAcceptanceState state) =>
        state is PolicyAcceptanceState.NeverAccepted or PolicyAcceptanceState.UpdateRequired;
}
