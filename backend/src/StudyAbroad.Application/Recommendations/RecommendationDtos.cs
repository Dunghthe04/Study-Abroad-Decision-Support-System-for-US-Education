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
        SchoolInfoDto? School = null,    // thông tin trường để giao diện hiện thẻ trường; null ở kết quả cũ đã lưu
        string? Block = null,            // khối trên màn 9: reach | match | safety | aid | other
        bool FarReach = false,           // Khó vào xa: đã Khó vào mà trường còn nhận ít du học sinh
        decimal? RankScore = null,       // X: điểm xếp hạng theo mức quan tâm (0–1)
        IReadOnlyList<string>? Why = null,     // mã lý do: GPA_LOW, SAT_IN_RANGE, BUDGET_OK, INTL_LOW...
        IReadOnlyList<string>? Flags = null,   // cờ dữ liệu: COST_UNKNOWN, C7_DEFAULT, ENGLISH_MIN_UNKNOWN...
        decimal? OverBudgetUsd = null,   // số tiền vượt ngân sách mỗi năm (khối aid)
        decimal? CostFit = null,         // F: (ngân sách − chi phí)/ngân sách, 0–1; dùng khi đổi nút ưu tiên
        decimal? AidFit = null,          // H: học bổng TB du học sinh / chi phí, 0–1
        decimal? Quality = null,         // Q: TB tỉ lệ giữ chân và tốt nghiệp, 0–1
        decimal? GpaPosition = null,     // GPA cao hơn khoảng bao nhiêu % sinh viên trường (0–100)
        decimal? SatPosition = null,     // vị trí SAT trong trường (25 = mốc 25%, 75 = mốc 75%)
        decimal? EcAdjustment = null);   // điểm cộng/trừ do ngoại khóa ở trường này (−6..+6)

    //Lời tư vấn AI viết cho 1 trường (gọi khi học sinh mở chi tiết trường)
    public record SchoolAdviceDto(
        string Summary,
        IReadOnlyList<string> Strengths,
        IReadOnlyList<string> Improvements,
        string Model);

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
        decimal? MinDuolingo,
        decimal? AvgGpa4 = null,    // GPA trung bình sinh viên trúng tuyển (thang 4)
        int? Sat25 = null,          // mốc SAT 25% và 75% của sinh viên trúng tuyển
        int? Sat75 = null);

    public record RecommendationResultDto(
        Guid Id,
        DateTime CreatedAt,
        string StudyLevel,
        IReadOnlyList<RecommendationItemDto> Items,
        IReadOnlyList<string> Warnings);  // ví dụ: "chưa quy đổi được GPA"
}
