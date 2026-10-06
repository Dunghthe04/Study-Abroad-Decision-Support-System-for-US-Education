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
        bool AiExplained);      // false = giải thích theo mẫu, true = AI viết (giai đoạn 4)

    public record RecommendationResultDto(
        Guid Id,
        DateTime CreatedAt,
        string StudyLevel,
        IReadOnlyList<RecommendationItemDto> Items,
        IReadOnlyList<string> Warnings);   // ví dụ: "chưa quy đổi được GPA"
}
