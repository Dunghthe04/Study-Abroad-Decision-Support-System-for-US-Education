namespace StudyAbroad.Domain.Constants;

/// <summary>[UC-103] Các loại chính sách và điều khoản admin quản lý. Mỗi loại có nhiều phiên bản, tại một thời điểm chỉ một bản đang hiệu lực.</summary>
public static class PolicyKinds
{
    /// <summary>Điều khoản sử dụng (trang /terms).</summary>
    public const string TermsOfUse = "terms_of_use";
    /// <summary>Chính sách bảo mật và quyền riêng tư (trang /privacy).</summary>
    public const string PrivacyPolicy = "privacy_policy";
    /// <summary>Nội dung phụ huynh đồng ý cho hệ thống xử lý dữ liệu của con dưới 16 tuổi (Luật BVDLCN 91/2025 Điều 24).</summary>
    public const string ChildDataConsent = "child_data_consent";
    /// <summary>Nội quy diễn đàn.</summary>
    public const string ForumRules = "forum_rules";

    public static readonly string[] All = [TermsOfUse, PrivacyPolicy, ChildDataConsent, ForumRules];
}

/// <summary>[UC-103] Vòng đời một phiên bản: nháp (sửa được) → đang hiệu lực (khóa) → hết hiệu lực (khi có bản mới công bố).</summary>
public static class PolicyStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Retired = "retired";

    public static readonly string[] All = [Draft, Published, Retired];
}
