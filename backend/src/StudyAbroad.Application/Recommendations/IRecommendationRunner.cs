using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations
{
    /// <summary>Gợi ý trường và tư vấn từng trường cho một người dùng. RecommendationService làm thật; chat gọi qua interface này để test được.</summary>
    public interface IRecommendationRunner
    {
        Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct);
        Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct);
        Task<SchoolExplanation> ExplainAsync(Guid userId, string code, CancellationToken ct);
    }
}
