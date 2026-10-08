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
        string? CategoryReason = null,   // vì sao trường thuộc nhóm này (code dựng từ số liệu); null ở kết quả cũ
        IReadOnlyList<string>? Strengths = null,    // ưu điểm của hồ sơ với trường này
        IReadOnlyList<string>? Weaknesses = null);  // nhược điểm của hồ sơ với trường này

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
        IReadOnlyList<string> Warnings,   // ví dụ: "chưa quy đổi được GPA"
        ExtracurricularSummaryDto? Extracurricular = null,   // null ở kết quả cũ lưu trước khi có bước chấm ngoại khóa
        StudentSummaryDto? Student = null);                  // hồ sơ dùng cho lần lọc này; null ở kết quả cũ

    //Hồ sơ học sinh dùng để lọc, để giao diện hiện "Học thuật (GPA, SAT, IELTS) · Ngân sách · Ngoại khóa"
    public record StudentSummaryDto(
        string? Major,
        decimal? Gpa4,                   // thang 4
        int? Sat,
        decimal? Ielts,
        decimal? Toefl,
        decimal? Duolingo,
        decimal? AnnualBudgetUsd,
        decimal? ExtracurricularScore);  // 0–4

    //Điểm ngoại khóa dùng cho lần lọc này. Score = null: chưa tính được, SAW bỏ tiêu chí ngoại khóa
    public record ExtracurricularSummaryDto(
        decimal? Score,                  // 0–4
        bool Fresh,                      // false = advisor lỗi, đang dùng điểm lần trước
        bool AiUsed,                     // false = vai trò đọc bằng từ khóa
        IReadOnlyList<ExtracurricularScoreItemDto> Activities);

    //Một hoạt động sau khi chấm: thuộc tính AI đọc ra + điểm theo công thức
    public record ExtracurricularScoreItemDto(
        string Id,                       // id của profile_activities
        string Name,
        string Role,                     // member | deputy | head | founder
        bool ReputableOrg,
        int ImpactLevel,                 // 1 trường … 5 quốc tế
        decimal Quality,                 // 0–1
        decimal Points,                  // đóng góp vào điểm tổng
        bool Counted,                    // false = ngoài 4 hoạt động (3 giải) tốt nhất
        string Kind = "activity");       // activity | award
}
