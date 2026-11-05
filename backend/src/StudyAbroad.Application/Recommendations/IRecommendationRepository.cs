using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    public interface IRecommendationRepository
    {
        Task AddAsync(Recommendation recommendation, CancellationToken ct = default);
        // Dùng ở mốc 4.5, chưa cần viết API.
        Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct = default);

        Task<IReadOnlyList<RecommendSchoolInput>> ListSchoolsAsync(string studyLevel, CancellationToken ct = default);
        //Tìm trường theo code, cấp độ
        Task<RecommendSchoolInput?> FindSchoolAsync(string code, string studyLevel, CancellationToken ct = default);

    }
}
