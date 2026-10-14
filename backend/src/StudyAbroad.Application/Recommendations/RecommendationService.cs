using System.Text.Json;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations
{
    public interface IRecommendationService
    {
        //Tạo 1 recommendation
        Task<RecommendationResultDto?> CreateAsync(Guid userId, CancellationToken ct = default);

        //Lấy ra recommendation gần đây nhất
        Task<RecommendationResultDto?> GetLatestAsync(Guid userId, CancellationToken ct = default);
    }

    //Luồng gợi ý trường (GOI_Y_TRUONG_AI_CACH_THUC_HIEN): lọc cứng → lượt ① ngoại khóa → lượt ② chấm trường → lượt ③ chọn danh sách
    public class RecommendationService(IRecommendationRepository repository, IRecommendationAi ai) : IRecommendationService
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly int[] BalancedPref = [2, 2, 2];   // nút "Cân bằng": mặc định khi mở màn 9
        private const int AverageTier = 3;                        // dùng tạm khi lượt ① lỗi: điều chỉnh ngoại khóa = 0

        public async Task<RecommendationResultDto?> CreateAsync(Guid userId, CancellationToken ct = default)
        {
            //1. Hồ sơ
            var profile = await repository.GetProfileAsync(userId, ct);
            if (profile is null) return null;
            if (!StudyLevels.IsValid(profile.TargetLevel))
                return Empty(profile.TargetLevel, ["Hồ sơ chưa chọn bậc học muốn đi. Hãy cập nhật hồ sơ trước khi gợi ý trường."]);
            if (profile.TargetLevel != StudyLevels.Undergraduate)
                return Empty(profile.TargetLevel, ["Hiện công thức gợi ý mới áp dụng cho bậc đại học."]);

            var settings = RecommendSettings.Parse(await repository.GetSettingJsonAsync(RecommendSettings.SettingKey, ct));
            var warnings = new List<string>();

            //2. Lọc cứng (code, không dùng AI)
            var candidates = await repository.GetSchoolCandidatesAsync(profile.TargetLevel, ct);
            var cipFamilies = candidates.SelectMany(c => c.Majors).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cipMajor = RecommendationInputs.ToCipMajor(profile.IntendedMajor, cipFamilies);
            if (!string.IsNullOrWhiteSpace(profile.IntendedMajor) && cipMajor is null)
                warnings.Add($"Chưa đối chiếu được ngành \"{profile.IntendedMajor}\" với danh mục ngành của trường, tạm chưa lọc theo ngành.");

            var criteria = RecommendationInputs.ToCriteria(profile, cipMajor);
            var passed = HardFilter.Apply(criteria, candidates, settings.BudgetTolerance)
                .Where(r => r.Outcome != FilterOutcome.Excluded)
                .OrderBy(r => r.Outcome)                                   // trường trong ngân sách trước khối học bổng
                .ThenBy(r => r.School.Code, StringComparer.Ordinal)        // cố định thứ tự để chạy lại ra cùng kết quả
                .Take(settings.MaxCandidates)
                .ToList();
            if (passed.Count == 0)
                return Empty(profile.TargetLevel, [.. warnings, "Không có trường nào phù hợp với ngành, bang, ngân sách và tiếng Anh hiện tại. Hãy thử nới điều kiện."]);

            //3. Lượt ①: bậc ngoại khóa (1 lần cho cả hồ sơ)
            var activities = await repository.GetActivitiesAsync(profile.Id, ct);
            AiEcResult? ec = null;
            try
            {
                ec = await ai.ReadExtracurricularAsync(RecommendationInputs.ToEcRequest(activities, DateOnly.FromDateTime(DateTime.UtcNow)), ct);
            }
            catch (RecommendationAiException)
            {
                warnings.Add("Chưa chấm được ngoại khóa, tạm tính ở mức trung bình (bậc 3).");
            }
            var tier = ec?.Tier ?? AverageTier;

            //4. Lượt ②: chấm từng trường (advisor tự chia lô 8 trường)
            var flags = new Dictionary<string, IReadOnlyList<string>>();
            var schools = passed.Select(r =>
            {
                var school = AiSchoolMapper.ToAi(r.School, out var dataFlags);
                flags[r.School.Code] = [.. r.Flags, .. dataFlags];
                return school;
            }).ToList();
            var sat = profile.Sat is decimal s ? (int?)Math.Round(s) : null;
            AiScoreResult score;
            try
            {
                score = await ai.ScoreAsync(new AiScoreRequest(new AiStudent(profile.OverallGpa, sat, profile.BudgetMaxUsd, tier), schools), ct);
            }
            catch (RecommendationAiException)
            {
                return Empty(profile.TargetLevel, [.. warnings, "AI chấm trường tạm thời không phản hồi. Hãy thử lại sau ít phút."]);
            }
            if (score.Failed.Count > 0)
                warnings.Add($"Có {score.Failed.Count} trường AI chưa chấm được, tạm chưa đưa vào danh sách.");

            //5. Lượt ③: xếp theo mức quan tâm (mặc định Cân bằng), chia khối, mã lý do
            var byCode = passed.ToDictionary(r => r.School.Code);
            var pickItems = score.Items
                .Where(i => byCode.ContainsKey(i.Code))
                .Select(i => new AiPickItem(i.Code, i.G2,
                    byCode[i.Code].Outcome == FilterOutcome.AidBlock ? "AID" : "MAIN",   // khối học bổng lấy từ lọc cứng
                    i.F, i.H, i.Q, i.Gap, i.Pg, i.Ps, i.Adj, i.R))
                .ToList();
            if (pickItems.Count == 0)
                return Empty(profile.TargetLevel, [.. warnings, "AI chưa chấm được trường nào. Hãy thử lại sau ít phút."]);

            var pickRequest = new AiPickRequest(BalancedPref, RecommendationInputs.NeedAidCode(criteria.AidNeed), pickItems);
            AiPickResult pick;
            try
            {
                pick = await ai.PickAsync(pickRequest, ct);
            }
            catch (RecommendationAiException)
            {
                // Dự phòng: chia khối, xếp hạng, mã lý do bằng code theo đúng luật của skill_pick
                pick = PickFallback.Pick(pickRequest, settings.PerCategory);
                warnings.Add("AI chưa sắp được danh sách, hệ thống đã tự sắp theo công thức.");
            }

            //6. Ghép kết quả AI với thông tin trường trong DB
            var scored = score.Items.ToDictionary(i => i.Code);
            (string Block, IReadOnlyList<AiPickEntry> Entries)[] blocks =
                [("reach", pick.Reach), ("match", pick.Match), ("safety", pick.Safety), ("aid", pick.Aid), ("other", pick.Other)];
            var items = blocks
                .SelectMany(b => b.Entries.Select(e => (b.Block, Entry: e)))
                .Where(x => byCode.ContainsKey(x.Entry.Code) && scored.ContainsKey(x.Entry.Code))
                .Select((x, i) => ToItem(i + 1, x.Block, x.Entry, byCode[x.Entry.Code], scored[x.Entry.Code], flags[x.Entry.Code]))
                .ToList();

            //7. Lưu kèm phiên bản skill và model để tái lập
            var entity = new Recommendation
            {
                UserId = userId,
                StudentProfileId = profile.Id,
                StudyLevel = profile.TargetLevel,
                CriteriaJson = JsonSerializer.Serialize(new
                {
                    criteria,
                    tier,
                    extracurricular = ec,
                    pref = BalancedPref,
                    skills = new { ec = ec?.SkillVersion, score = score.SkillVersion, pick = pick.SkillVersion },
                    model = score.Model
                }, Json),
                ItemsJson = JsonSerializer.Serialize(items, Json),
                AlgorithmVersion = $"ai3-ec{ec?.SkillVersion ?? "none"}-score{score.SkillVersion}-pick{pick.SkillVersion}"
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

        private static RecommendationResultDto Empty(string level, IReadOnlyList<string> warnings) =>
            new(Guid.Empty, DateTime.UtcNow, level, [], warnings);

        private static RecommendationItemDto ToItem(int rank, string block, AiPickEntry pick, FilterResult filter, AiScoreItem score, IReadOnlyList<string> flags)
        {
            var s = filter.School;
            return new RecommendationItemDto(
                rank,
                s.UniversityId,
                s.OfferingId,
                s.Code,
                s.Name,
                s.State,
                Category: score.G2 switch { "R" or "RF" => "reach", "M" => "match", "S" => "safety", _ => "insufficient_data" },
                Score: score.S,
                TotalCostUsd: s.TotalCostUsd,
                CostUnknown: s.TotalCostUsd is null,
                English: EnglishCode(filter.Flags),
                OpenAdmission: score.G2 == "O",
                Reason: ReasonTexts.Join(pick.Why),
                AiExplained: true,
                School: new SchoolInfoDto(s.City, s.State, s.Control, s.Website, s.AcceptanceRate, s.InternationalStudents,
                    s.SatPolicy, s.TuitionUsd, s.LivingUsd, s.FeesUsd, s.MinIelts, s.MinToefl, s.MinDuolingo, s.AvgGpa4, s.Sat25, s.Sat75),
                Block: block,
                FarReach: score.G2 == "RF",
                RankScore: pick.X,
                Why: pick.Why,
                Flags: flags,
                OverBudgetUsd: score.Gap);
        }

        //Tiếng Anh: lọc cứng đã loại trường thiếu điểm, còn lại là đạt hoặc chưa so được
        private static string EnglishCode(IReadOnlyList<string> flags) =>
            flags.Contains(FilterFlags.EnglishNoScore) ? "no_score"
            : flags.Contains(FilterFlags.EnglishMinUnknown) || flags.Contains(FilterFlags.EnglishNotComparable) ? "unknown"
            : "met";
    }

    //Mã lý do (lượt ③) → câu hiển thị, theo bảng cuối mục 8 file AI_CACH_THUC_HIEN
    public static class ReasonTexts
    {
        private static readonly Dictionary<string, string> Texts = new()
        {
            ["GPA_HIGH"] = "GPA cao hơn phần lớn sinh viên của trường",
            ["GPA_MID"] = "GPA ngang sinh viên của trường",
            ["GPA_LOW"] = "GPA thấp hơn phần lớn sinh viên của trường",
            ["SAT_ABOVE"] = "SAT cao hơn khoảng 25–75 của trường",
            ["SAT_IN_RANGE"] = "SAT nằm trong khoảng 25–75 của trường",
            ["SAT_BELOW"] = "SAT thấp hơn khoảng 25–75 của trường",
            ["BUDGET_OK"] = "Chi phí trong ngân sách",
            ["NEED_AID"] = "Cần học bổng lớn",
            ["INTL_LOW"] = "Trường nhận ít du học sinh so với tỉ lệ chung",
            ["EC_PLUS"] = "Ngoại khóa là lợi thế ở trường này",
            ["EC_MINUS"] = "Ngoại khóa cần bổ sung ở trường này",
        };

        public static string Join(IEnumerable<string> codes)
        {
            var sentences = codes.Select(c => Texts.TryGetValue(c, out var text) ? text : c).ToList();
            return sentences.Count == 0 ? "" : string.Join(". ", sentences) + ".";
        }
    }
}
