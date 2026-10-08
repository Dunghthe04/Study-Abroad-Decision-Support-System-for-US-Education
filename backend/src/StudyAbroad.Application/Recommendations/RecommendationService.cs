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
    public class RecommendationService(IRecommendationRepository repository, IRecommendationAi ai, ExtracurricularScoring scoring) : IRecommendationService
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

            //Điểm ngoại khóa: LLM đọc hoạt động thành thuộc tính, công thức của nhóm tính điểm 0–4
            var extracurricular = await ScoreExtracurricularAsync(profile, settings, warnings, ct);

            if (profile.Ielts is null && profile.Toefl is null && profile.Duolingo is null)
                warnings.Add("Chưa có điểm tiếng Anh (IELTS/TOEFL/Duolingo), chưa kiểm tra điều kiện tiếng Anh.");

            var student = new StudentSnapshot(
                        profile.TargetLevel,
                        profile.IntendedMajor,
                        gpa4,
                        profile.Sat is { } sat ? (int)Math.Round(sat) : null,
                        profile.AnnualBudgetUsd,
                        profile.PreferredStates,
                        extracurricular.Score,
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
            //Lý do xếp nhóm, ưu điểm, nhược điểm do code dựng từ số liệu (luôn đúng, không chờ AI)
            var items = guarded.Select((g, i) => (g, i, parts: ReasonBreakdown.Build(student, g.School, settings.GpaBand))).Select(x => new RecommendationItemDto(
                Rank: x.i + 1,
                x.g.School.Candidate.UniversityId,
                x.g.School.Candidate.OfferingId,
                x.g.School.Candidate.Code,
                x.g.School.Candidate.Name,
                x.g.School.Candidate.State,
                ReasonTemplate.CategoryCode(x.g.School.AdmissionCategory),
                x.g.School.Score,
                x.g.School.TotalCostUsd,
                x.g.School.CostUnknown,
                ReasonTemplate.EnglishCode(x.g.School.English),
                x.g.School.OpenAdmission,
                 x.g.Reason,
                x.g.AiExplained,
                ToSchoolInfo(x.g.School.Candidate),
                x.parts.CategoryReason,
                x.parts.Strengths,
                x.parts.Weaknesses)).ToList();

            //7. Lưu
            var entity = new Recommendation
            {
                UserId = userId,
                StudentProfileId = profile.Id,
                StudyLevel = student.StudyLevel,
                CriteriaJson = JsonSerializer.Serialize(new { student, settings, extracurricular }, Json),
                ItemsJson = JsonSerializer.Serialize(items, Json),
                AlgorithmVersion = AlgorithmVersion
            };
            await repository.AddAsync(entity, ct);
            return new RecommendationResultDto(entity.Id, entity.CreatedAt, entity.StudyLevel, items, warnings, extracurricular, ToSummary(student));
        }

        //Hoạt động không đổi và lần trước AI đã đọc được → dùng lại kết quả cũ, không gọi LLM.
        //Đổi thì gọi advisor (có dự phòng: điểm cũ hoặc bỏ tiêu chí), có gì khác lần trước thì lưu lại.
        private async Task<ExtracurricularSummaryDto> ScoreExtracurricularAsync(
            StudentProfile profile, RecommendSettings settings, List<string> warnings, CancellationToken ct)
        {
            var activities = await repository.GetActivitiesAsync(profile.Id, ct);
            var inputs = ExtracurricularInputs.From(activities, DateOnly.FromDateTime(DateTime.UtcNow));
            var hash = ExtracurricularInputs.Hash(inputs);

            var previous = ReadAnalysis(await repository.GetLatestAnalysisAsync(profile.Id, ExtracurricularInputs.AnalysisKind, ct));
            if (previous is { AiUsed: true } && previous.InputHash == hash)
                return Summary(previous.Score, fresh: true, aiUsed: true, previous.Activities, inputs);

            var outcome = await scoring.ScoreAsync(inputs, profile.ExtracurricularScore, settings.ExtracurricularTimeoutSeconds, ct);
            if (outcome.Warning is not null) warnings.Add(outcome.Warning);

            if (outcome.Fresh && outcome.Score is { } score &&
                (previous is null || previous.InputHash != hash || previous.Score != score || previous.AiUsed != outcome.AiUsed))
            {
                var analysis = new ExtracurricularAnalysis(hash, score, outcome.AiUsed, outcome.Activities);
                await repository.SaveExtracurricularAsync(new AnalysisResult
                {
                    StudentProfileId = profile.Id,
                    Kind = ExtracurricularInputs.AnalysisKind,
                    ModelVersion = ExtracurricularInputs.ModelVersion,
                    ResultJson = JsonSerializer.Serialize(analysis, Json),
                }, score, ct);
            }
            return Summary(outcome.Score, outcome.Fresh, outcome.AiUsed, outcome.Activities, inputs);
        }

        private static ExtracurricularAnalysis? ReadAnalysis(AnalysisResult? result)
        {
            if (result is null) return null;
            try { return JsonSerializer.Deserialize<ExtracurricularAnalysis>(result.ResultJson, Json); }
            catch (JsonException) { return null; }   // dữ liệu cũ hỏng thì coi như chưa có, tính lại
        }

        //Ghép điểm từng hoạt động với tên hoạt động để giao diện hiển thị
        private static ExtracurricularSummaryDto Summary(decimal? score, bool fresh, bool aiUsed,
            IReadOnlyList<ScoredActivity> scored, IReadOnlyList<ExtracurricularActivityInput> inputs)
        {
            var names = inputs.ToDictionary(i => i.Id, i => i.Name);
            return new ExtracurricularSummaryDto(score, fresh, aiUsed, scored.Select(a => new ExtracurricularScoreItemDto(
                a.Id, names.GetValueOrDefault(a.Id, ""), a.Role, a.ReputableOrg, a.ImpactLevel, a.Quality, a.Points, a.Counted, a.Kind)).ToList());
        }
        private static SchoolInfoDto ToSchoolInfo(SchoolCandidate c) => new(
    c.City, c.State, c.Control, c.Website, c.AcceptanceRate, c.InternationalStudents, c.SatPolicy,
    c.TuitionUsd, c.LivingUsd, c.FeesUsd, c.MinIelts, c.MinToefl, c.MinDuolingo, c.AvgGpa4, c.Sat25, c.Sat75);

        private static StudentSummaryDto ToSummary(StudentSnapshot s) =>
            new(s.Major, s.Gpa4, s.Sat, s.Ielts, s.Toefl, s.Duolingo, s.AnnualBudgetUsd, s.ExtracurricularScore);

        //Gọi LLM có giới hạn thời gian. Trả null khi AI tắt, không có trường, LLM lỗi hoặc quá thời gian
        private async Task<IReadOnlyList<AiPick>?> TryRankWithAiAsync(StudentSnapshot student, IReadOnlyList<ScoredSchool> scored, RecommendSettings settings, CancellationToken ct)
        {
            if (!settings.AiEnabled || scored.Count == 0) return null;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(settings.AiTimeoutSeconds));
            try
            {
                var response = await ai.RankAsync(ToAiRequest(student, scored, settings.GpaBand), timeout.Token);
                return response.Items;
            }
            catch (Exception) when (!ct.IsCancellationRequested)   // người dùng tự hủy request thì không nuốt lỗi
            {
                return null;
            }
        }

        //Chỉ gửi số liệu cần thiết cho LLM, không gửi thông tin cá nhân
        private static AiRankRequest ToAiRequest(StudentSnapshot s, IReadOnlyList<ScoredSchool> scored, decimal gpaBand) => new(
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
                ReasonTemplate.EnglishCode(r.English),
                AdmissionCategorizer.Basis(s.Gpa4, s.Sat, r.Candidate.AvgGpa4, r.Candidate.Sat25, r.Candidate.Sat75, gpaBand))).ToList());

        public async Task<RecommendationResultDto?> GetLatestAsync(Guid userId, CancellationToken ct = default)
        {
            var latest = await repository.GetLatestRecommendationAsync(userId, ct);
            if (latest is null) return null;
            var items = JsonSerializer.Deserialize<List<RecommendationItemDto>>(latest.ItemsJson, Json) ?? [];
            return new RecommendationResultDto(latest.Id, latest.CreatedAt, latest.StudyLevel, items, [], ReadSummary(latest.CriteriaJson), ReadStudent(latest.CriteriaJson));
        }

        //Bảng ngoại khóa lưu trong criteria_json; kết quả cũ chưa có thì null
        private static ExtracurricularSummaryDto? ReadSummary(string criteriaJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(criteriaJson);
                return doc.RootElement.TryGetProperty("extracurricular", out var ec)
                    ? ec.Deserialize<ExtracurricularSummaryDto>(Json)
                    : null;
            }
            catch (JsonException) { return null; }
        }

        //Hồ sơ lúc lọc lưu trong criteria_json (khóa "student")
        private static StudentSummaryDto? ReadStudent(string criteriaJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(criteriaJson);
                return doc.RootElement.TryGetProperty("student", out var st) && st.Deserialize<StudentSnapshot>(Json) is { } s
                    ? ToSummary(s)
                    : null;
            }
            catch (JsonException) { return null; }
        }
    }
}
