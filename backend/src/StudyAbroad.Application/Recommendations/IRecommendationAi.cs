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
        string English,// met | below_min | no_score | unknown
        string? CategoryBasis = null);// gpa | sat | gpa_sat: tiêu chí quyết định nhóm; null = chưa đủ dữ liệu

    public record AiRankRequest(AiStudentInput Student, IReadOnlyList<AiSchoolInput> Schools);

    //LLM trả về : mã trường + giải thích; thứ tự cuối cùng vẫn là thứ tự SAW
    public record AiRankResponse(IReadOnlyList<AiPick> Items);

    /// <summary>Gọi LLM viết giải thích cho tập ứng viên CRM đã xếp hạng. Cài đặt thật ở Infrastructure (gọi sang advisor).</summary>
    public interface IRecommendationAi
    {
        Task<AiRankResponse> RankAsync(AiRankRequest request,CancellationToken ct= default);
    }


}
