using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StudyAbroad.Domain.Constants;
using System.Text.Json;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    public interface IRecommendationService
    {
        //Tạo 1 recommendation
        Task<RecommendationResultDto?> CreateAsync(Guid userId, CancellationToken ct = default);

        //Lấy ra recommendation gần đây nhất
        Task<RecommendationResultDto?> GetLatestAsync(Guid userId, CancellationToken ct = default);
    }
    public class RecommendationService(IRecommendationRepository repository) : IRecommendationService
    {
        public const string AlgorithmVersion = "crm-saw-v1";
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        public async Task<RecommendationResultDto?> CreateAsync(Guid userId, CancellationToken ct = default)
        {
            //1. Hồ sơ
            var profile = await repository.GetProfileAsync(userId,ct);
            if (profile == null) return null;
            var warnings = new List<string>();
            if (!StudyLevels.IsValid(profile.TargetLevel))
                return new RecommendationResultDto(Guid.Empty, DateTime.UtcNow, profile.TargetLevel, [],
                    ["Hồ sơ chưa chọn bậc học muốn đi. Hãy cập nhật hồ sơ trước khi gợi ý trường."]);


            //2. Cấu hình
            var settings = RecommendSettings.Parse(await repository.GetSettingJsonAsync(RecommendSettings.SettingKey, ct));

            //3. Chuẩn hóa hồ sơ
            //overall_gpa luôn là thang 4 (phân tích học thuật của Đức quy đổi từ bảng điểm)
            var gpa4 = profile.OverallGpa;
            if (gpa4 is null)
                warnings.Add("Chưa có GPA thang 4 (chưa phân tích bảng điểm), tạm chỉ xét SAT.");

            if (profile.ExtracurricularScore is null)
                warnings.Add("Chưa có điểm ngoại khóa từ phân tích hồ sơ, chưa xét tiêu chí ngoại khóa.");

            if (profile.Ielts is null && profile.Toefl is null && profile.Duolingo is null)
                warnings.Add("Chưa có điểm tiếng Anh (IELTS/TOEFL/Duolingo), chưa kiểm tra điều kiện tiếng Anh.");

            var student = new StudentSnapshot(
                        profile.TargetLevel,
                        profile.IntendedMajor,
                        gpa4,
                        profile.Sat is { } sat ? (int)Math.Round(sat) : null,
                        profile.AnnualBudgetUsd,
                        profile.PreferredStates,
                        profile.ExtracurricularScore,
                        profile.Ielts,
                        profile.Toefl,
                        profile.Duolingo);

            //4. lấy trường ứng viên + chấm điểm
            var candidates = await repository.GetSchoolCandidatesAsync(student.StudyLevel, ct);
            var scored = RecommendationScorer.Recommend(student, candidates, settings);
            if (scored.Count == 0)
            {
                warnings.Add("Không có trường nào phù hợp với ngành, bang và ngân sách hiện tại. Hãy thử nới điều kiện.");
            }
            //5. Đổi sang DTO + giải thích
            var items = scored.Select((s, i) => new RecommendationItemDto(
                Rank: i + 1,
                s.Candidate.UniversityId,
                s.Candidate.OfferingId,
                s.Candidate.Code,
                s.Candidate.Name,
                s.Candidate.State,
                ReasonTemplate.CategoryCode(s.AdmissionCategory),
                s.Score,
                s.TotalCostUsd,
                s.CostUnknown,
                ReasonTemplate.EnglishCode(s.English),
                s.OpenAdmission,
                ReasonTemplate.Build(student, s),
                AiExplained: false)).ToList();
            //6. Lưu
            var entity = new Recommendation
            {
                UserId = userId,
                StudentProfileId = profile.Id,
                StudyLevel = student.StudyLevel,
                CriteriaJson = JsonSerializer.Serialize(new { student, settings }, Json),
                ItemsJson = JsonSerializer.Serialize(items, Json),
                AlgorithmVersion = AlgorithmVersion
            };
            await repository.AddAsync(entity, ct);
            return new RecommendationResultDto(entity.Id, entity.CreatedAt, entity.StudyLevel, items, warnings);
        }


        public async Task<RecommendationResultDto?> GetLatestAsync(Guid userId, CancellationToken ct = default)
        {
            var latest = await repository.GetLatestRecommendationAsync(userId, ct);
            if (latest is null) return null;
            var items = JsonSerializer.Deserialize<List<RecommendationItemDto>>(latest.ItemsJson, Json) ?? [];
            return new RecommendationResultDto(latest.Id, latest.CreatedAt, latest.StudyLevel, items, []);
        }
    }
}
