namespace StudyAbroad.Domain.Constants;

/// <summary>[USAS-2029] Hằng số cho ước tính chi phí du học (UC-26).</summary>
public static class CostConstants
{
    /// <summary>
    /// Khóa trong app_settings, JSON: { "visaFeeUsd", "visaSourceUrl", "sevisFeeUsd", "sevisSourceUrl", "retrievedAt" }.
    /// Chưa có khóa hoặc thiếu số thì ước tính ghi "chưa có", không tự điền.
    /// </summary>
    public const string ReferenceFeesAppSettingKey = "cost.reference_fees";

    /// <summary>
    /// Khóa trong app_settings cho 4 tham số ước tính chi phí UC-26:
    /// { "visaIntegrityFeeUsd", "annualEscalationRate", "bankSellRateSpread", "undergraduateYears" }.
    /// </summary>
    public const string CostParametersAppSettingKey = "cost.parameters";

    /// <summary>Số năm học của chương trình đại học, dùng cho tổng chi phí 4 năm.</summary>
    public const int UndergraduateYears = 4;

    /// <summary>Phí Visa Integrity Fee mặc định (USD, thu một lần năm đầu theo UC-26 / UC-83) khi chưa cấu hình trong app_settings.</summary>
    public const decimal DefaultVisaIntegrityFeeUsd = 250m;

    /// <summary>Tỷ lệ tăng chi phí hằng năm cho công thức 4 năm (1 + r)^(k - 1) (UC-26 / UC-83).</summary>
    public const decimal AnnualCostEscalationRate = 0.04m;

    /// <summary>Biên độ giá bán ra của ngân hàng cộng lên tỷ giá trung bình thị trường khi quy đổi USD→VND ở UC-26 (UC-83 / UC-65).</summary>
    public const decimal BankSellRateSpread = 0.015m;

    /// <summary>Biên ngân sách 10% dùng để gắn nhãn "Trong biên 10%" (UC-26, khớp UC-22).</summary>
    public const decimal BudgetTolerance = 0.10m;
}
