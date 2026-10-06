using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    //Hồ sơ gửi cho LLM, chỉ có số liệu, ko có tên, email, sdt
    public record AiStudentInput(
        string StudyLevel,
        string? Major,
        decimal? Gpa4,
        int? Sat,
        decimal? AnnualBudgetUsd,
        decimal? Ielts,
        decimal? Toefl,
        decimal? Duolingo,
        decimal? ExtracurricularScore);

    //Một trường trong tập ứng viên CRM gửi cho LLM; Category do CRM quyết định, LLM không được đổi
    public record AiSchoolInput(
        string Code,
        string Name,
        string? State,
        string Category, // reach | match | safety | insufficient_data
        decimal? AvgGpa4,
        int? Sat25,
        int? Sat75,
        decimal? TuitionUsd,
        decimal? TotalCostUsd,
        string English);// met | below_min | no_score | unknown

    public record AiRankRequest(AiStudentInput Student, IReadOnlyList<AiSchoolInput> Schools);

    //LLM trả về : danh sách mã trường + giải thích theo thứ tự LLM xếp
    public record AiRankResponse(IReadOnlyList<AiPick> Items);

    /// <summary>Gọi LLM xếp lại tập ứng viên và viết giải thích. Cài đặt thật ở Infrastructure (gọi sang advisor).</summary>
    public interface IRecommendationAi
    {
        Task<AiRankResponse> RankAsync(AiRankRequest request,CancellationToken ct= default);
    }


}
