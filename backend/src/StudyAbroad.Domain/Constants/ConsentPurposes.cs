namespace StudyAbroad.Domain.Constants;

public static class ConsentPurposes
{
    /// <summary>Phụ huynh đồng ý cho hệ thống xử lý dữ liệu cá nhân của con dưới 16 tuổi (Luật BVDLCN 91/2025 Điều 24).</summary>
    public const string ChildDataProcessing = "child_data_processing";

    /// <summary>Phiên bản nội dung đồng ý hiện hành, lưu vào consents.policy_version. Đổi nội dung thì tăng phiên bản.</summary>
    public const string ChildDataPolicyVersion = "2026-10";
}
