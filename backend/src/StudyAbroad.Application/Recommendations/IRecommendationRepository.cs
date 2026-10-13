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

        /// Lưu hồ sơ gợi ý trường cho học sinh
        Task AddAsync(Recommendation recommendation, CancellationToken ct = default);

        //Lần gợi ý gần nhất của user, null nếu chưa có
        Task<Recommendation?> GetLatestRecommendationAsync(Guid userId, CancellationToken ct=default);

        //Hoạt động ngoại khóa, kinh nghiệm, giải thưởng trong hồ sơ
        Task<IReadOnlyList<ProfileActivity>> GetActivitiesAsync(Guid studentProfileId, CancellationToken ct = default);

        //Kết quả phân tích mới nhất theo loại (vd. "extracurricular"), null nếu chưa có
        Task<AnalysisResult?> GetLatestAnalysisAsync(Guid studentProfileId, string kind, CancellationToken ct = default);

        //Lưu kết quả chấm ngoại khóa và cập nhật student_profiles.extracurricular_score
        Task SaveExtracurricularAsync(AnalysisResult analysis, decimal score, CancellationToken ct = default);

    }
}
