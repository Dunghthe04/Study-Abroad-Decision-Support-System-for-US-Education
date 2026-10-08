namespace StudyAbroad.Application.Recommendations
{
    //Một hoạt động gửi sang advisor: chỉ chữ học sinh nhập về hoạt động, không có tên, email, sđt của học sinh
    public record ExtracurricularActivityInput(
        string Id,
        string Name,
        string? Role,          // chữ học sinh nhập, ví dụ "Chủ nhiệm CLB"
        string? Organization,
        string? Description,
        int? ImpactLevel,      // học sinh tự khai: 1 trường … 5 quốc tế; null = không khai, LLM đọc từ mô tả
        int? Months);

    public record ExtracurricularScoreRequest(IReadOnlyList<ExtracurricularActivityInput> Activities);

    //Kết quả từng hoạt động: thuộc tính LLM đọc ra + điểm theo công thức của nhóm
    public record ScoredActivity(
        string Id,
        string Role,           // member | deputy | head | founder
        bool ReputableOrg,
        int ImpactLevel,       // sau khi LLM đối chiếu mô tả (không cao hơn mức học sinh khai)
        decimal Quality,
        decimal Points,
        bool Counted);         // false = ngoài 4 hoạt động tốt nhất

    //AiUsed = false: LLM lỗi, advisor đọc vai trò bằng từ khóa
    public record ExtracurricularScoreResponse(decimal Score, bool AiUsed, IReadOnlyList<ScoredActivity> Activities);

    /// <summary>Gọi advisor tính điểm ngoại khóa 0–4. Cài đặt thật ở Infrastructure.</summary>
    public interface IExtracurricularAi
    {
        Task<ExtracurricularScoreResponse> ScoreAsync(ExtracurricularScoreRequest request, CancellationToken ct = default);
    }
}
