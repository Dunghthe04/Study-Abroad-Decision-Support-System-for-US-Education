using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Application.Profile.Flow;
using GpaModes = StudyAbroad.Domain.Constants.ProfileConstants.GpaModes;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// C1/C2 của kế hoạch đồng bộ chat–form: đổi qua lại giữa nháp form và profile của chat.
/// Profile chat có dạng { khóa: { value, status } }, khóa theo DRAFT_KEYS của advisor (profile_sync.py).
/// Điều kiện phải giữ: ToChatProfile(ApplyChatProfile(x)) == x, nếu không merge_profile sẽ ghi đè chat mỗi lượt.
/// </summary>
public static class ChatProfileMapper
{
    // Thứ tự khóa khi trả về, trùng DRAFT_KEYS
    public static readonly IReadOnlyList<string> DraftKeys =
    [
        "current_level", "current_grade", "graduation_year", "target_level", "major", "gpa", "sat", "act", "english",
        "english_plan", "standardized_plan", "certifications", "budget", "scholarship", "activities", "awards", "preferred_states", "school_type",
        "education_system", "funding_source", "intake",
    ];

    // Trạng thái có giá trị; none / unknown / declined thì nháp không có chỗ lưu
    private static readonly string[] ValueStatuses = ["confirmed", "said_in_B0"];

    private const string English = "english";
    private const string Standardized = "standardized";
    private const string OtherType = "other";
    private const string Unknown = "unknown";

    /// <summary>
    /// C1: nháp → profile chat, giá trị có trong nháp mang trạng thái confirmed.
    /// previous là profile chat lượt trước: khóa nháp để trống mà lượt trước là none / unknown / declined
    /// thì giữ lại, để chat không hỏi lại "em có chứng chỉ không" sau mỗi lượt.
    /// </summary>
    public static JsonObject ToChatProfile(ProfileFlowDraftDto draft, JsonObject? previous = null)
    {
        var step1 = draft.Step1;
        var certs = draft.Step3?.Certifications ?? [];
        var items = draft.Step5?.Activities ?? [];
        var values = new Dictionary<string, JsonNode?>
        {
            ["current_level"] = Text(step1?.CurrentEducationLevel),
            ["current_grade"] = Text(step1?.CurrentGrade),
            ["graduation_year"] = step1?.ExpectedGraduationYear != null ? JsonValue.Create(step1.ExpectedGraduationYear.Value) : null,
            ["target_level"] = Text(step1?.TargetDegreeLevel),
            ["major"] = Text(step1?.TargetMajor),
            ["gpa"] = Gpa(draft.Step2),
            ["sat"] = TestScore(certs, "sat"),
            ["act"] = TestScore(certs, "act"),
            ["english"] = MainEnglish(certs),
            ["english_plan"] = draft.Step3?.EnglishPlanned == true
                ? PlanNode(draft.Step3.EnglishPlannedDate, draft.Step3.EnglishPlannedTestType, draft.Step3.EnglishPlannedTargetScore)
                : null,
            ["standardized_plan"] = draft.Step3?.StandardizedPlanned == true
                ? PlanNode(draft.Step3.StandardizedPlannedDate, draft.Step3.StandardizedPlannedTestType, draft.Step3.StandardizedPlannedTargetScore)
                : null,
            // Cả danh sách thẻ chứng chỉ như form, để chat thêm được nhiều chứng chỉ
            ["certifications"] = ListOrNull(certs.Select(c => JsonSerializer.SerializeToNode(c)!)),
            ["budget"] = Text(draft.Step4?.AnnualBudgetTier),
            ["scholarship"] = Text(draft.Step4?.NeedFinancialAid),
            ["activities"] = ListOrNull(items.Where(i => !IsAward(i)).Select(ActivityToChat)),
            ["awards"] = ListOrNull(items.Where(IsAward).Select(AwardToChat)),
            ["preferred_states"] = ListOrNull((step1?.TargetStates ?? []).Select(s => (JsonNode)JsonValue.Create(s)!)),
            ["school_type"] = Text(step1?.TargetSchoolType),
            ["education_system"] = Text(step1?.EducationSystem),
            ["funding_source"] = Text(draft.Step4?.FundingSource),
            ["intake"] = Text(step1?.TargetIntake),
        };

        var profile = new JsonObject();
        foreach (var key in DraftKeys)
        {
            if (values[key] is { } value)
                profile[key] = new JsonObject { ["value"] = value, ["status"] = "confirmed" };
            // Ngành: cờ "Chưa rõ" của nháp quyết định, form bỏ "Chưa rõ" thì chat hỏi lại
            else if (key == "major" && step1?.MajorUndecided is { } undecided)
            {
                if (undecided)
                    profile[key] = new JsonObject { ["value"] = Unknown, ["status"] = Unknown };
            }
            // Form tích "sẽ thi sau" là đã trả lời chưa có chứng chỉ tiếng Anh / chuẩn hóa, chat không đòi hỏi lại
            else if (key == "english" && draft.Step3?.EnglishPlanned == true)
                profile[key] = new JsonObject { ["value"] = null, ["status"] = "none" };
            else if ((key == "sat" || key == "act") && draft.Step3?.StandardizedPlanned == true)
                profile[key] = new JsonObject { ["value"] = null, ["status"] = "none" };
            else if (KeepFromPrevious(key, previous?[key]))
                profile[key] = previous![key]!.DeepClone();
        }
        return profile;
    }

    /// <summary>
    /// C2: ghi các khóa chat vừa đổi vào nháp, các ô khác giữ nguyên.
    /// Khóa không có giá trị (none / unknown / declined / đã xóa) thì xóa ô tương ứng trong nháp.
    /// scale là bảng grade_scale.10 đang dùng để quy đổi GPA thang 10; null thì bảng mặc định.
    /// </summary>
    public static ProfileFlowDraftDto ApplyChatProfile(
        ProfileFlowDraftDto draft, JsonObject profile, IEnumerable<string> changedKeys, GradeScaleConfigDto? scale = null)
    {
        // certifications là cả danh sách nên ghi sau cùng, đè lên english / sat / act (chỉ là điểm rút ra từ danh sách)
        foreach (var key in changedKeys.OrderBy(k => k == "certifications"))
        {
            var value = ValueOf(profile[key]);
            var step1 = draft.Step1 ?? new FlowGoalDraftDto();
            var step3 = draft.Step3 ?? new FlowCertificationDraftDto();
            var step4 = draft.Step4 ?? new FlowFinancialDraftDto();
            draft = key switch
            {
                "current_level" => draft with { Step1 = step1 with { CurrentEducationLevel = Str(value) } },
                "current_grade" => draft with { Step1 = step1 with { CurrentGrade = Str(value) } },
                "graduation_year" or "expected_graduation_year" => draft with
                {
                    Step1 = step1 with
                    {
                        ExpectedGraduationYear = value is not null ? value.GetValue<int>() : null
                    }
                },
                "target_level" => draft with { Step1 = step1 with { TargetDegreeLevel = Str(value) } },
                "major" => draft with
                {
                    Step1 = step1 with { TargetMajor = Str(value), MajorUndecided = StatusOf(profile[key]) == Unknown },
                },
                "preferred_states" => draft with { Step1 = step1 with { TargetStates = value?.AsArray().Select(s => s!.GetValue<string>()).ToList() } },
                "school_type" => draft with { Step1 = step1 with { TargetSchoolType = Str(value) } },
                "education_system" => draft with { Step1 = step1 with { EducationSystem = Str(value) } },
                "intake" => draft with { Step1 = step1 with { TargetIntake = Str(value) } },
                "funding_source" => draft with { Step4 = step4 with { FundingSource = Str(value) } },
                "gpa" => draft with { Step2 = ApplyGpa(draft.Step2, value, scale) },
                "sat" or "act" => draft with { Step3 = step3 with { Certifications = PutCert(step3.Certifications, Standardized, key, value is null ? null : (value.GetValue<decimal>(), null)) } },
                "english" => draft with { Step3 = step3 with { Certifications = PutEnglish(step3.Certifications, value) } },
                "certifications" => draft with { Step3 = step3 with { Certifications = CertsFromChat(value) } },
                "english_plan" => draft with
                {
                    Step3 = step3 with
                    {
                        EnglishPlanned = value?["planned"]?.GetValue<bool>() ?? false,
                        EnglishPlannedDate = Str(value?["date"]),
                        EnglishPlannedTestType = Str(value?["test_type"]),
                        EnglishPlannedTargetScore = value?["target_score"]?.GetValue<decimal>(),
                    },
                },
                "standardized_plan" => draft with
                {
                    Step3 = step3 with
                    {
                        StandardizedPlanned = value?["planned"]?.GetValue<bool>() ?? false,
                        StandardizedPlannedDate = Str(value?["date"]),
                        StandardizedPlannedTestType = Str(value?["test_type"]),
                        StandardizedPlannedTargetScore = value?["target_score"]?.GetValue<decimal>(),
                    },
                },
                "budget" => draft with { Step4 = step4 with { AnnualBudgetTier = Str(value) } },
                "scholarship" => draft with { Step4 = step4 with { NeedFinancialAid = Str(value) } },
                "activities" => draft with { Step5 = new FlowActivitiesDraftDto(PutItems(draft.Step5?.Activities, award: false, value)) },
                "awards" => draft with { Step5 = new FlowActivitiesDraftDto(PutItems(draft.Step5?.Activities, award: true, value)) },
                _ => draft,
            };
        }

        if (draft.Step1 is { } s1 && s1.ExpectedGraduationYear == null)
        {
            var guessed = GuessGraduationYear(s1.CurrentEducationLevel, s1.CurrentGrade);
            if (guessed.HasValue)
            {
                draft = draft with { Step1 = s1 with { ExpectedGraduationYear = guessed.Value } };
            }
        }

        return draft;
    }

    public static int? GuessGraduationYear(string? level, string? grade, DateTime? now = null)
    {
        var lastGrade = level switch
        {
            "high_school" => 12,
            "middle_school" => 9,
            "college_university" => 4,
            "master" => 2,
            _ => (int?)null,
        };
        if (lastGrade == null || string.IsNullOrWhiteSpace(grade)) return null;

        var current = now ?? DateTime.UtcNow;
        var schoolYearEnd = current.Month >= 8 ? current.Year + 1 : current.Year;
        if (grade == "Năm cuối") return schoolYearEnd;

        var match = System.Text.RegularExpressions.Regex.Match(grade, @"(?:Lớp|Năm)\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
        {
            return schoolYearEnd + Math.Max(0, lastGrade.Value - n);
        }
        return null;
    }

    // Kế hoạch 3.1: chế độ lấy theo gpaMode (TranscriptGpa.Mode đoán cho nháp cũ)
    private static JsonObject? Gpa(FlowTranscriptDraftDto? step2)
    {
        if (TranscriptGpa.Mode(step2) == GpaModes.Transcript)
            return TranscriptGpa.SubjectAverage10(step2) is { } average ? GpaNode(average, 10, GpaModes.Transcript) : null;
        if (step2?.ManualGpa10Scale is { } gpa10) return GpaNode(gpa10, 10, GpaModes.Overall);
        if (step2?.CalculatedGpa4Scale is { } gpa4) return GpaNode(gpa4, 4, GpaModes.Overall);
        return null;
    }

    private static JsonObject GpaNode(decimal value, int scale, string source) =>
        new() { ["value"] = value, ["scale"] = scale, ["source"] = source };

    // GPA đang tính từ học bạ thì chat không ghi đè (kế hoạch 3.1). Chọn học bạ mà chưa có điểm môn
    // thì không mất gì, cho chat ghi GPA tổng để khỏi hỏi đi hỏi lại
    private static FlowTranscriptDraftDto? ApplyGpa(FlowTranscriptDraftDto? step2, JsonNode? value, GradeScaleConfigDto? scale)
    {
        if (Gpa(step2)?["source"]?.GetValue<string>() == GpaModes.Transcript) return step2;
        step2 = (step2 ?? new FlowTranscriptDraftDto()) with { GpaMode = GpaModes.Overall };
        if (value is null) return step2 with { ManualGpa10Scale = null, CalculatedGpa4Scale = null };

        var gpa = value["value"]!.GetValue<decimal>();
        return value["scale"]!.GetValue<int>() == 10
            ? step2 with { ManualGpa10Scale = gpa, CalculatedGpa4Scale = TranscriptGpa.To4(gpa, scale) }
            : step2 with { ManualGpa10Scale = null, CalculatedGpa4Scale = gpa };
    }

    // SAT / ACT: ưu tiên điểm thi thật, nếu chưa có thì lấy điểm mục tiêu; lấy điểm cao nhất (ApplyLegacyCertificationColumns)
    private static JsonNode? TestScore(List<FlowCertificationItemDto> certs, string type)
    {
        var matches = certs.Where(c => c.Type == type && c.Overall is not null).ToList();
        var actual = matches.Where(c => !c.IsTarget).ToList();
        var candidates = actual.Count > 0 ? actual : matches;
        return candidates.Max(c => c.Overall) is { } best ? JsonValue.Create((int)best) : null;
    }

    // Chứng chỉ tiếng Anh chính: ưu tiên thi thật; ngày thi mới nhất, rồi điểm cao nhất (ApplyLegacyCertificationColumns)
    private static JsonObject? MainEnglish(List<FlowCertificationItemDto> certs)
    {
        var matches = certs
            .Where(c => c.Category == English && c.Overall is not null && !string.IsNullOrEmpty(c.Type) && c.Type != OtherType)
            .ToList();
        var actual = matches.Where(c => !c.IsTarget).ToList();
        var candidates = actual.Count > 0 ? actual : matches;
        var main = candidates
            .OrderByDescending(c => c.TestDate ?? "", StringComparer.Ordinal) // yyyy-MM so được bằng chuỗi
            .ThenByDescending(c => c.Overall)
            .FirstOrDefault();
        return main is null
            ? null
            : new JsonObject { ["type"] = main.Type, ["value"] = main.Overall, ["date"] = Text(main.TestDate) };
    }

    private static List<FlowCertificationItemDto> PutEnglish(List<FlowCertificationItemDto>? certs, JsonNode? value)
    {
        // Học sinh nói không có chứng chỉ tiếng Anh thì bỏ hết chứng chỉ tiếng Anh
        if (value is null) return (certs ?? []).Where(c => c.Category != English).ToList();
        var type = value["type"]!.GetValue<string>();
        return PutCert(certs, English, type, (value["value"]!.GetValue<decimal>(), Str(value["date"])));
    }

    // Thay chứng chỉ cùng loại, giữ các chứng chỉ khác; score null thì bỏ chứng chỉ loại đó
    private static List<FlowCertificationItemDto> PutCert(
        List<FlowCertificationItemDto>? certs, string category, string type, (decimal Overall, string? Date)? score)
    {
        var list = (certs ?? []).ToList();
        var index = list.FindIndex(c => c.Type == type);
        if (score is not { } s)
        {
            list.RemoveAll(c => c.Type == type);
            return list;
        }

        // Điểm thành phần cũ không còn khớp điểm tổng mới nên bỏ
        var item = index >= 0
            ? list[index] with { Overall = s.Overall, TestDate = s.Date, Sections = null }
            : new FlowCertificationItemDto(Guid.NewGuid().ToString(), category, type, Overall: s.Overall, TestDate: s.Date);
        if (index >= 0) list[index] = item;
        else list.Add(item);
        return list;
    }

    // Thay toàn bộ chứng chỉ bằng danh sách của chat; thẻ thiếu id thì tạo id mới
    private static List<FlowCertificationItemDto> CertsFromChat(JsonNode? value) =>
        (value?.AsArray() ?? [])
            .Select(node => node.Deserialize<FlowCertificationItemDto>()!)
            .Select(c => string.IsNullOrEmpty(c.Id) ? c with { Id = Guid.NewGuid().ToString() } : c)
            .ToList();

    private static JsonNode ActivityToChat(FlowActivityDraftDto item)
    {
        var node = new JsonObject
        {
            ["name"] = item.Name,
            ["category"] = "activity",
            ["role"] = item.Role,
            ["impactLevel"] = item.ImpactLevel,
            ["startDate"] = item.StartDate,
            ["endDate"] = item.EndDate,
            ["isOngoing"] = item.IsOngoing,
        };
        if (!string.IsNullOrWhiteSpace(item.Description))
            node["description"] = item.Description;
        return node;
    }

    private static JsonNode AwardToChat(FlowActivityDraftDto item)
    {
        var node = new JsonObject
        {
            ["name"] = item.Name,
            ["category"] = "achievement",
            ["impactLevel"] = item.ImpactLevel,
            ["year"] = item.Year,
        };
        if (!string.IsNullOrWhiteSpace(item.Description))
            node["description"] = item.Description;
        return node;
    }

    // Thay toàn bộ hoạt động (hoặc giải thưởng) bằng danh sách của chat, giữ id theo vị trí và giữ loại còn lại
    private static List<FlowActivityDraftDto> PutItems(List<FlowActivityDraftDto>? items, bool award, JsonNode? value)
    {
        var all = items ?? [];
        var oldIds = all.Where(i => IsAward(i) == award).Select(i => i.Id).ToList();
        var fromChat = (value?.AsArray() ?? [])
            .Select((node, i) =>
            {
                var id = i < oldIds.Count && oldIds[i] is { } existingId ? existingId : Guid.NewGuid().ToString();
                return NodeToActivity(node, award, id);
            });
        var kept = all.Where(i => IsAward(i) != award);
        return award ? kept.Concat(fromChat).ToList() : fromChat.Concat(kept).ToList();
    }

    private static FlowActivityDraftDto NodeToActivity(JsonNode? node, bool award, string id)
    {
        if (node is JsonObject obj)
        {
            var name = obj["name"]?.GetValue<string>();
            var cat = obj["category"]?.GetValue<string>() ?? (award ? "achievement" : "activity");
            var role = obj["role"]?.GetValue<string>();
            var impact = obj["impactLevel"]?.GetValue<string>() ?? obj["impact_level"]?.GetValue<string>();
            var start = obj["startDate"]?.GetValue<string>() ?? obj["start_date"]?.GetValue<string>();
            var end = obj["endDate"]?.GetValue<string>() ?? obj["end_date"]?.GetValue<string>();
            var ongoing = obj["isOngoing"]?.GetValue<bool>() ?? obj["is_ongoing"]?.GetValue<bool>() ?? false;
            var desc = obj["description"]?.GetValue<string>();
            var year = obj["year"]?.GetValue<int?>();
            var duration = obj["durationMonths"]?.GetValue<int?>() ?? obj["duration_months"]?.GetValue<int?>();

            return new FlowActivityDraftDto(
                Id: id,
                Name: name,
                Category: cat,
                Role: role,
                ImpactLevel: impact,
                StartDate: start,
                EndDate: end,
                IsOngoing: ongoing,
                DurationMonths: duration,
                Year: year,
                Description: desc
            );
        }

        var dto = node.Deserialize<FlowActivityDraftDto>() ?? new FlowActivityDraftDto();
        return dto with { Id = id };
    }

    private static bool KeepFromPrevious(string key, JsonNode? previous)
    {
        if (previous is not JsonObject field) return false;
        var status = field["status"]?.GetValue<string>();
        if (!ValueStatuses.Contains(status)) return true;
        // Nháp chỉ có englishPlanned / standardizedPlanned = false, không phân biệt "chưa hỏi" với "chưa có kế hoạch"
        return (key == "english_plan" || key == "standardized_plan") && field["value"]?["planned"]?.GetValue<bool>() == false;
    }

    private static string? StatusOf(JsonNode? field) => (field as JsonObject)?["status"]?.GetValue<string>();

    private static JsonNode? ValueOf(JsonNode? field) =>
        field is JsonObject f && ValueStatuses.Contains(f["status"]?.GetValue<string>()) ? f["value"] : null;

    private static bool IsAward(FlowActivityDraftDto item) =>
        string.Equals(item.Category, "achievement", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.Category, "award", StringComparison.OrdinalIgnoreCase);

    private static JsonObject PlanNode(string? date, string? testType, decimal? targetScore)
    {
        var node = new JsonObject { ["planned"] = true, ["date"] = Text(date) };
        if (!string.IsNullOrWhiteSpace(testType)) node["test_type"] = testType;
        if (targetScore.HasValue) node["target_score"] = targetScore.Value;
        return node;
    }

    private static JsonNode? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value);

    private static string? Str(JsonNode? value) => value?.GetValue<string>();

    private static JsonArray? ListOrNull(IEnumerable<JsonNode> nodes)
    {
        var list = nodes.ToArray();
        return list.Length == 0 ? null : new JsonArray(list);
    }
}
