namespace StudyAbroad.Domain.Constants;

public static class ConsentPurposes
{
    /// <summary>Phụ huynh đồng ý cho hệ thống xử lý dữ liệu cá nhân của con dưới 16 tuổi (Luật BVDLCN 91/2025 Điều 24).</summary>
    public const string ChildDataProcessing = "child_data_processing";

    /// <summary>[UC-104] Tài khoản đồng ý Điều khoản sử dụng. Trùng chuỗi với loại tài liệu cùng tên của UC-103; policy_version ghi phiên bản đã đọc.</summary>
    public const string TermsOfUse = PolicyKinds.TermsOfUse;

    /// <summary>[UC-104] Tài khoản đồng ý Chính sách bảo mật. Trùng chuỗi với loại tài liệu cùng tên của UC-103.</summary>
    public const string PrivacyPolicy = PolicyKinds.PrivacyPolicy;

    /// <summary>Phiên bản nội dung đồng ý hiện hành, lưu vào consents.policy_version. Đổi nội dung thì tăng phiên bản.</summary>
    public const string ChildDataPolicyVersion = "2026-10";

    /// <summary>
    /// Phiên bản chính sách đồng ý ĐẦU TIÊN có nêu rõ việc quản trị viên có thể đọc hội thoại đã che thông tin cá nhân để đánh giá chất lượng AI (UC-90).
    /// Hội thoại của người dưới 16 tuổi chỉ hiện cho admin khi phụ huynh đã đồng ý bản từ phiên bản này trở đi. So sánh theo thứ tự chữ (yyyy-MM).
    /// Phiên bản hiện hành (ChildDataPolicyVersion) còn thấp hơn mốc này cho đến khi chính sách được bổ sung, nên hiện chưa hội thoại nào của trẻ hiện ra.
    /// Khi bổ sung chính sách thì tăng ChildDataPolicyVersion lên bằng mốc này (hoặc cao hơn).
    /// </summary>
    public const string QualityReviewMinPolicyVersion = "2026-11";
}
