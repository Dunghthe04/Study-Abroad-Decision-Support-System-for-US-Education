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

        //AI tư vấn 1 trường trong lần gợi ý gần nhất. null = chưa có gợi ý hoặc trường không có trong kết quả
        Task<SchoolAdviceDto?> ExplainSchoolAsync(Guid userId, Guid offeringId, CancellationToken ct = default);
    }

    //Luồng gợi ý trường: lọc cứng (C#) → bậc ngoại khóa (advisor: AI đọc mô tả, Python tính bậc) → chấm trường (C#) → chia khối, xếp hạng (C#).
    //Phần công thức chạy bằng code vì AI tự tính chỉ đúng 33–48% trên bộ đánh giá 216 cặp (docs/GOI_Y_TRUONG_TODO.md, mục 4.3).
    public class RecommendationService(IRecommendationRepository repository, IRecommendationAi ai) : IRecommendationService
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly int[] BalancedPref = [2, 2, 2];   // nút "Cân bằng": mặc định khi mở màn 9
        private const int AverageTier = 3;                        // dùng tạm khi chưa chấm được ngoại khóa: điều chỉnh = 0
        public const string AlgorithmVersion = "rule1";           // công thức GOI_Y_TRUONG_TONG_QUAN bản 2 chạy bằng C#

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
                .ThenBy(r => r.School.Code, StringComparer.Ordinal)        // cố định thứ tự để chạy lại ra cùng kết quả; chấm mọi trường qua lọc, không giới hạn số lượng
                .ToList();
            if (passed.Count == 0)
                return Empty(profile.TargetLevel, [.. warnings, "Không có trường nào phù hợp với ngành, bang, ngân sách và tiếng Anh hiện tại. Hãy thử nới điều kiện."]);
            if (passed.All(r => r.Outcome == FilterOutcome.AidBlock))
                warnings.Add($"Ngân sách {profile.BudgetMaxUsd:N0} USD/năm thấp hơn chi phí của mọi trường phù hợp. " +
                             "Các trường dưới đây chỉ đi được nếu có học bổng lớn; bạn có thể cân nhắc nới ngân sách.");

            //3. Bậc ngoại khóa (gọi advisor 1 lần: AI đọc vai trò, phạm vi; Python tính bậc)
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

            //4. Chấm từng trường (tầng 2 + F/H/Q), cùng công thức với advisor/evals/recommendation/reference.py
            var flags = new Dictionary<string, IReadOnlyList<string>>();
            var sat = profile.Sat is decimal s ? (int?)Math.Round(s) : null;
            var student = new AiStudent(profile.OverallGpa, sat, profile.BudgetMaxUsd, tier);
            var scoreItems = passed.Select(r =>
            {
                var school = AiSchoolMapper.ToAi(r.School, out var dataFlags);
                flags[r.School.Code] = [.. r.Flags, .. dataFlags];
                return SchoolScorer.Score(student, school);
            }).ToList();

            //5. Xếp theo mức quan tâm (mặc định Cân bằng), chia khối 3/5/4 + khối học bổng, mã lý do
            var byCode = passed.ToDictionary(r => r.School.Code);
            var pickItems = scoreItems
                .Select(i => new AiPickItem(i.Code, i.G2,
                    byCode[i.Code].Outcome == FilterOutcome.AidBlock ? "AID" : "MAIN",   // khối học bổng lấy từ lọc cứng
                    i.F, i.H, i.Q, i.Gap, i.Pg, i.Ps, i.Adj, i.R))
                .ToList();
            var pick = SchoolPicker.Pick(new AiPickRequest(BalancedPref, RecommendationInputs.NeedAidCode(criteria.AidNeed), pickItems), settings.PerCategory);

            //6. Ghép kết quả với thông tin trường trong DB
            var scored = scoreItems.ToDictionary(i => i.Code);
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
                    skillEc = ec?.SkillVersion,
                    model = ec?.Model
                }, Json),
                ItemsJson = JsonSerializer.Serialize(items, Json),
                AlgorithmVersion = $"{AlgorithmVersion}-ec{ec?.SkillVersion ?? "none"}"
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

        public async Task<SchoolAdviceDto?> ExplainSchoolAsync(Guid userId, Guid offeringId, CancellationToken ct = default)
        {
            var latest = await repository.GetLatestRecommendationAsync(userId, ct);
            var profile = await repository.GetProfileAsync(userId, ct);
            if (latest is null || profile is null) return null;
            var items = JsonSerializer.Deserialize<List<RecommendationItemDto>>(latest.ItemsJson, Json) ?? [];
            if (items.FirstOrDefault(i => i.OfferingId == offeringId) is not { } item) return null;

            // Bậc ngoại khóa dùng đúng bậc của lần gợi ý đó (lưu trong criteria_json)
            int? tier = null;
            using (var criteria = JsonDocument.Parse(latest.CriteriaJson))
                if (criteria.RootElement.TryGetProperty("tier", out var t) && t.TryGetInt32(out var v)) tier = v;

            var sat = profile.Sat is decimal s ? (int?)Math.Round(s) : null;
            var group = item.FarReach ? "Khó vào xa" : item.Category switch
            {
                "reach" => "Khó vào",
                "match" => "Vừa sức",
                "safety" => "An toàn hơn",
                _ => "Chưa đủ dữ liệu"
            };
            var request = new AiExplainRequest(
                new AiExplainStudent(profile.OverallGpa, sat, profile.Ielts, profile.Toefl, profile.Duolingo,
                    profile.BudgetMaxUsd, tier, profile.IntendedMajor),
                new AiExplainSchool(item.Name, item.State, item.School?.Control, group, item.Block == "aid",
                    item.School?.AcceptanceRate * 100, item.School?.AvgGpa4, item.School?.Sat25, item.School?.Sat75,
                    item.GpaPosition, item.SatPosition, item.TotalCostUsd, item.OverBudgetUsd, item.EcAdjustment));

            var advice = await ai.ExplainAsync(request, ct);   // lỗi → RecommendationAiException, controller trả 503
            return new SchoolAdviceDto(advice.Summary, advice.Strengths, advice.Improvements, advice.Model);
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
                AiExplained: false,     // câu lý do ghép từ mã; AI viết tư vấn sẽ làm sau
                School: new SchoolInfoDto(s.City, s.State, s.Control, s.Website, s.AcceptanceRate, s.InternationalStudents,
                    s.SatPolicy, s.TuitionUsd, s.LivingUsd, s.FeesUsd, s.MinIelts, s.MinToefl, s.MinDuolingo, s.AvgGpa4, s.Sat25, s.Sat75),
                Block: block,
                FarReach: score.G2 == "RF",
                RankScore: pick.X,
                Why: pick.Why,
                Flags: flags,
                OverBudgetUsd: score.Gap,
                CostFit: score.F,
                AidFit: score.H,
                Quality: score.Q,
                GpaPosition: score.Pg,
                SatPosition: score.Ps,
                EcAdjustment: score.Adj);
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
