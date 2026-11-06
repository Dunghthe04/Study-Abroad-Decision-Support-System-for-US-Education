using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations
{
    /// <summary>Gợi ý trường và tư vấn từng trường cho một người dùng. RecommendationService làm thật; chat gọi qua interface này để test được.</summary>
    public interface IRecommendationRunner
    {
        Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct);
        Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct);
        // overrides: giá trị giả định học sinh đang xem (null là hồ sơ thật); basis: mô tả giả định đó cho lời tư vấn
        Task<SchoolExplanation> ExplainAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null, string? basis = null);

        /// <summary>Thẻ giải thích một trường do code dựng (số đã lưu và mã lý do), không gọi AI: để khung chat hiện ngay trước lời tư vấn.</summary>
        Task<ExplainSchoolDto> ExplainCardAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null);
        Task<IReadOnlyList<SchoolOption>> ListSchoolOptionsAsync(Guid userId, CancellationToken ct);
        Task<NameMatch> FindSchoolsAsync(Guid userId, string text, CancellationToken ct);
        Task<SchoolGoalResult> GoalSeekAsync(Guid userId, string code, string? target, CancellationToken ct);
        Task<SimulationResult> SimulateAsync(Guid userId, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct);
    }
}
