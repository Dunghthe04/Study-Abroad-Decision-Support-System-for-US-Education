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
    public class RecommendationService(IRecommendationRepository repository, IRecommendationAi ai) : IRecommendationService
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
            //5. LLM viết giải thích cho từng trường (không đổi thứ tự SAW); lỗi or tắt = null ==> giải thích soạn sẵn
            var picks = await TryRankWithAiAsync(student, scored, settings, ct);
            if (picks is null && settings.AiEnabled && scored.Count > 0)
                warnings.Add("AI tạm thời chưa phản hồi, danh sách và giải thích theo kết quả chấm điểm.");

            //6. Kiểm tra đầu ra LLM(mã trường, con số) rồi đổi sang Dto
            var guarded = AiOutputGuard.Apply(picks ?? [], scored, student);
            var items = guarded.Select((g, i) => new RecommendationItemDto(
                Rank: i + 1,
                g.School.Candidate.UniversityId,
                g.School.Candidate.OfferingId,
                g.School.Candidate.Code,
                g.School.Candidate.Name,
                g.School.Candidate.State,
                ReasonTemplate.CategoryCode(g.School.AdmissionCategory),
                g.School.Score,
                g.School.TotalCostUsd,
                g.School.CostUnknown,
                ReasonTemplate.EnglishCode(g.School.English),
                g.School.OpenAdmission,
                 g.Reason,
                g.AiExplained,
                ToSchoolInfo(g.School.Candidate))).ToList();

            //7. Lưu
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
        private static SchoolInfoDto ToSchoolInfo(SchoolCandidate c) => new(
    c.City, c.State, c.Control, c.Website, c.AcceptanceRate, c.InternationalStudents, c.SatPolicy,
    c.TuitionUsd, c.LivingUsd, c.FeesUsd, c.MinIelts, c.MinToefl, c.MinDuolingo);

        //Gọi LLM có giới hạn thời gian. Trả null khi AI tắt, không có trường, LLM lỗi hoặc quá thời gian
        private async Task<IReadOnlyList<AiPick>?> TryRankWithAiAsync(StudentSnapshot student, IReadOnlyList<ScoredSchool> scored, RecommendSettings settings, CancellationToken ct)
        {
            if (!settings.AiEnabled || scored.Count == 0) return null;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.AiTimeoutSeconds));
            try
            {
                var response = await ai.RankAsync(ToAiRequest(student, scored), timeout.Token);
                return response.Items;
            }
            catch (Exception) when (!ct.IsCancellationRequested)   // người dùng tự hủy request thì không nuốt lỗi
            {
                return null;
            }
        }

        //Chỉ gửi số liệu cần thiết cho LLM, không gửi thông tin cá nhân
        private static AiRankRequest ToAiRequest(StudentSnapshot s, IReadOnlyList<ScoredSchool> scored) => new(
            new AiStudentInput(s.StudyLevel, s.Major, s.Gpa4, s.Sat, s.AnnualBudgetUsd, s.Ielts, s.Toefl, s.Duolingo, s.ExtracurricularScore),
            scored.Select(r => new AiSchoolInput(
                r.Candidate.Code,
                r.Candidate.Name,
                r.Candidate.State,
                ReasonTemplate.CategoryCode(r.AdmissionCategory),
                r.Candidate.AvgGpa4,
                r.Candidate.Sat25,
                r.Candidate.Sat75,
                r.Candidate.TuitionUsd,
                r.TotalCostUsd,
                ReasonTemplate.EnglishCode(r.English))).ToList());

        public async Task<RecommendationResultDto?> GetLatestAsync(Guid userId, CancellationToken ct = default)
        {
            var latest = await repository.GetLatestRecommendationAsync(userId, ct);
            if (latest is null) return null;
            var items = JsonSerializer.Deserialize<List<RecommendationItemDto>>(latest.ItemsJson, Json) ?? [];
            return new RecommendationResultDto(latest.Id, latest.CreatedAt, latest.StudyLevel, items, []);
        }
    }
}
