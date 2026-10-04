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
        //Lấy hồ sơ học sinh để gợi ý trường
        Task<StudentProfile?> GetProfileAsync(Guid studentId, CancellationToken ct=default);

        //lấy cấu hình gợi ý trường (key = "recommend_settings")
        Task<string?> GetSettingJsonAsync(string key,CancellationToken ct=default);

        //Lấy danh sách trường ứng viên để gợi ý
        Task<IReadOnlyList<SchoolCandidate>> GetSchoolCandidatesAsync(string studyLevel, CancellationToken ct=default);

        /// <summary>ResultJson mới nhất của AI phân tích hồ sơ (kind = strengths_weaknesses), null nếu chưa có.</summary>
        Task<string?> GetLatestAnalysisJsonAsync(Guid studentProfileId, CancellationToken ct=default);
        /// Lưu hồ sơ gợi ý trường cho học sinh
        Task AddAsync(Recommendation recommendation, CancellationToken ct = default);

    }
}
