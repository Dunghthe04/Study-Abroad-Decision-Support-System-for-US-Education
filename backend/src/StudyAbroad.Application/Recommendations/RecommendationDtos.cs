namespace StudyAbroad.Application.Recommendations
{
    //Một trường trong danh sách gợi ý (cũng là 1 phần tử của recommendations.items_json)
    public record RecommendationItemDto(
        int Rank,
        Guid UniversityId,
        Guid OfferingId,
        string Code,
        string Name,
        string? State,
        string Category,        // reach | match | safety | insufficient_data
        decimal? Score,         // điểm xếp hạng nội bộ, KHÔNG phải xác suất đậu
        decimal? TotalCostUsd,
        bool CostUnknown,
        string English,         // met | below_min | no_score | unknown
        bool OpenAdmission,
         string Reason,
        bool AiExplained,       // false = giải thích theo mẫu, true = AI viết (giai đoạn 4)
        SchoolInfoDto? School = null);   // thông tin trường để giao diện hiện thẻ trường; null ở kết quả cũ đã lưu

    //Thông tin trường lấy thẳng từ database (không phụ thuộc học sinh)
    public record SchoolInfoDto(
        string? City,
        string? State,
        string? Control,            // public | private
        string? Website,
        decimal? AcceptanceRate,    // 0–1
        int? InternationalStudents,
        string? SatPolicy,          // required | optional | not_accepted
        decimal? TuitionUsd,
        decimal? LivingUsd,
        decimal? FeesUsd,
        decimal? MinIelts,
        decimal? MinToefl,
        decimal? MinDuolingo);

    public record RecommendationResultDto(
        Guid Id,
        DateTime CreatedAt,
        string StudyLevel,
        IReadOnlyList<RecommendationItemDto> Items,
        IReadOnlyList<string> Warnings);   // ví dụ: "chưa quy đổi được GPA"
}
