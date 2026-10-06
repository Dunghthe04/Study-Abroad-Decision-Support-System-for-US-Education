using StudyAbroad.Application.Grading;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
    public class RecommendationService(IRecommendationRepository repository, IGpaConverter gpaConverter) : IRecommendationService
    {
        public const string AlgorithmVersion = "crm-saw-v1";
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        public async Task<RecommendationResultDto?> CreateAsync(Guid userId, CancellationToken ct = default)
        {
            //1. Hồ sơ
            var profile = await repository.GetProfileAsync(userId,ct);
            if (profile == null) return null;
            var warnings = new List<string>();

            //2. Cấu hình
            var settings = RecommendSettings.Parse(await repository.GetSettingJsonAsync(RecommendSettings.SettingKey, ct));

            //3. Chuẩn hóa hồ sơ
            var gpa4 = await gpaConverter.ToGpa4Async(profile.OverallGpa, profile.GradeScale, ct);
            if (profile.OverallGpa is not null && gpa4 is null)
                warnings.Add($"Chưa quy đổi được GPA thang {profile.GradeScale} sang thang 4, tạm chỉ xét SAT.");

            //⚠ bảng phân tích lọc theo ID HỒ SƠ (profile.Id), không phải userId
            var extracurricular = ParseExtracurricular(
                await repository.GetLatestAnalysisJsonAsync(profile.Id, ct));

            if (extracurricular is null)
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
                        extracurricular,
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

        //Đọc "extracurricularScore" (0–10) từ ResultJson của AI phân tích hồ sơ (#7)
        public static decimal? ParseExtracurricular(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                       && doc.RootElement.TryGetProperty("extracurricularScore", out var v)
                       && v.ValueKind == JsonValueKind.Number   // giá trị dạng chữ thì TryGetDecimal ném lỗi, nên kiểm tra trước
                       && v.TryGetDecimal(out var score)
                    ? score
                    : null;
            }
            catch (JsonException)
            {
                return null;   // JSON hỏng → coi như chưa có điểm, không làm sập chức năng
            }
        }
    }
}
