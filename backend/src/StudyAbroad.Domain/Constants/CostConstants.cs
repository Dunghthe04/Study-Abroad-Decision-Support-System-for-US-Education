namespace StudyAbroad.Domain.Constants;

/// <summary>[USAS-2029] Hằng số cho ước tính chi phí du học (UC-26).</summary>
public static class CostConstants
{
    /// <summary>
    /// Khóa trong app_settings, JSON: { "visaFeeUsd", "visaSourceUrl", "sevisFeeUsd", "sevisSourceUrl", "retrievedAt" }.
    /// Chưa có khóa hoặc thiếu số thì ước tính ghi "chưa có", không tự điền.
    /// </summary>
    public const string ReferenceFeesAppSettingKey = "cost.reference_fees";

    /// <summary>Số năm học của chương trình đại học, dùng cho tổng chi phí 4 năm.</summary>
    public const int UndergraduateYears = 4;
}
