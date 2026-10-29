using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations
{
    /// <summary>Chạy gợi ý trường cho một người dùng. RecommendationService làm thật; chat gọi qua interface này để test được.</summary>
    public interface IRecommendationRunner
    {
        Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct);
    }
}
