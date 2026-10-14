namespace StudyAbroad.Infrastructure.Advisor;

public class AdvisorOptions
{
    public const string SectionName = "Advisor";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    // Đọc bảng điểm: model ảnh chép bảng rồi LLM bóc điểm, nhiều tệp nên lâu hơn các lời gọi khác
    public int OcrTimeoutSeconds { get; set; } = 600;

    // Gợi ý trường: lượt ② chấm tối đa 40 trường theo lô 8 (khoảng 30 giây/lô, lô lỗi được gọi lại 1 lần)
    public int RecommendTimeoutSeconds { get; set; } = 600;
}
