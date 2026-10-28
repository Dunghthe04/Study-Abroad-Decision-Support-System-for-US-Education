using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        "current_level", "current_grade", "target_level", "major", "gpa", "sat", "act", "english",
        "english_plan", "budget", "scholarship", "activities", "awards", "preferred_states", "school_type",
        "education_system", "funding_source", "intake",
    ];

    // Trạng thái có giá trị; none / unknown / declined thì nháp không có chỗ lưu
    private static readonly string[] ValueStatuses = ["confirmed", "said_in_B0"];

    private const string English = "english";
    private const string Standardized = "standardized";
    private const string OtherType = "other";

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
            ["target_level"] = Text(step1?.TargetDegreeLevel),
            ["major"] = Text(step1?.TargetMajor),
            ["gpa"] = Gpa(draft.Step2),
            ["sat"] = TestScore(certs, "sat"),
            ["act"] = TestScore(certs, "act"),
            ["english"] = MainEnglish(certs),
            ["english_plan"] = draft.Step3?.EnglishPlanned == true
                ? new JsonObject { ["planned"] = true, ["date"] = Text(draft.Step3.EnglishPlannedDate) }
                : null,
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
            else if (KeepFromPrevious(key, previous?[key]))
                profile[key] = previous![key]!.DeepClone();
        }
        return profile;
    }

    /// <summary>
    /// C2: ghi các khóa chat vừa đổi vào nháp, các ô khác giữ nguyên.
    /// Khóa không có giá trị (none / unknown / declined / đã xóa) thì xóa ô tương ứng trong nháp.
    /// </summary>
    public static ProfileFlowDraftDto ApplyChatProfile(ProfileFlowDraftDto draft, JsonObject profile, IEnumerable<string> changedKeys)
    {
        foreach (var key in changedKeys)
        {
            var value = ValueOf(profile[key]);
            var step1 = draft.Step1 ?? new FlowGoalDraftDto();
            var step3 = draft.Step3 ?? new FlowCertificationDraftDto();
            var step4 = draft.Step4 ?? new FlowFinancialDraftDto();
            draft = key switch
            {
                "current_level" => draft with { Step1 = step1 with { CurrentEducationLevel = Str(value) } },
                "current_grade" => draft with { Step1 = step1 with { CurrentGrade = Str(value) } },
                "target_level" => draft with { Step1 = step1 with { TargetDegreeLevel = Str(value) } },
                "major" => draft with { Step1 = step1 with { TargetMajor = Str(value) } },
                "preferred_states" => draft with { Step1 = step1 with { TargetStates = value?.AsArray().Select(s => s!.GetValue<string>()).ToList() } },
                "school_type" => draft with { Step1 = step1 with { TargetSchoolType = Str(value) } },
                "education_system" => draft with { Step1 = step1 with { EducationSystem = Str(value) } },
                "intake" => draft with { Step1 = step1 with { TargetIntake = Str(value) } },
                "funding_source" => draft with { Step4 = step4 with { FundingSource = Str(value) } },
                "gpa" => draft with { Step2 = ApplyGpa(draft.Step2, value) },
                "sat" or "act" => draft with { Step3 = step3 with { Certifications = PutCert(step3.Certifications, Standardized, key, value is null ? null : (value.GetValue<decimal>(), null)) } },
                "english" => draft with { Step3 = step3 with { Certifications = PutEnglish(step3.Certifications, value) } },
                "english_plan" => draft with
                {
                    Step3 = step3 with
                    {
                        EnglishPlanned = value?["planned"]?.GetValue<bool>() ?? false,
                        EnglishPlannedDate = Str(value?["date"]),
                    },
                },
                "budget" => draft with { Step4 = step4 with { AnnualBudgetTier = Str(value) } },
                "scholarship" => draft with { Step4 = step4 with { NeedFinancialAid = Str(value) } },
                "activities" => draft with { Step5 = new FlowActivitiesDraftDto(PutItems(draft.Step5?.Activities, award: false, value)) },
                "awards" => draft with { Step5 = new FlowActivitiesDraftDto(PutItems(draft.Step5?.Activities, award: true, value)) },
                _ => draft,
            };
        }
        return draft;
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
    private static FlowTranscriptDraftDto? ApplyGpa(FlowTranscriptDraftDto? step2, JsonNode? value)
    {
        if (Gpa(step2)?["source"]?.GetValue<string>() == GpaModes.Transcript) return step2;
        step2 = (step2 ?? new FlowTranscriptDraftDto()) with { GpaMode = GpaModes.Overall };
        if (value is null) return step2 with { ManualGpa10Scale = null, CalculatedGpa4Scale = null };

        var gpa = value["value"]!.GetValue<decimal>();
        return value["scale"]!.GetValue<int>() == 10
            ? step2 with { ManualGpa10Scale = gpa, CalculatedGpa4Scale = TranscriptGpa.To4(gpa) }
            : step2 with { ManualGpa10Scale = null, CalculatedGpa4Scale = gpa };
    }

    // SAT / ACT: lấy điểm cao nhất, giống ApplyLegacyCertificationColumns
    private static JsonNode? TestScore(List<FlowCertificationItemDto> certs, string type) =>
        certs.Where(c => c.Type == type && c.Overall is not null).Max(c => c.Overall) is { } best
            ? JsonValue.Create((int)best)
            : null;

    // Chứng chỉ tiếng Anh chính: ngày thi mới nhất, rồi điểm cao nhất (ApplyLegacyCertificationColumns)
    private static JsonObject? MainEnglish(List<FlowCertificationItemDto> certs)
    {
        var main = certs
            .Where(c => c.Category == English && c.Overall is not null && !string.IsNullOrEmpty(c.Type) && c.Type != OtherType)
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

    private static JsonNode ActivityToChat(FlowActivityDraftDto item) => new JsonObject
    {
        ["name"] = item.Name,
        ["category"] = "activity",
        ["role"] = item.Role,
        ["impactLevel"] = item.ImpactLevel,
        ["startDate"] = item.StartDate,
        ["endDate"] = item.EndDate,
        ["isOngoing"] = item.IsOngoing,
    };

    private static JsonNode AwardToChat(FlowActivityDraftDto item) => new JsonObject
    {
        ["name"] = item.Name,
        ["category"] = "achievement",
        ["impactLevel"] = item.ImpactLevel,
        ["year"] = item.Year,
    };

    // Thay toàn bộ hoạt động (hoặc giải thưởng) bằng danh sách của chat, giữ id theo vị trí và giữ loại còn lại
    private static List<FlowActivityDraftDto> PutItems(List<FlowActivityDraftDto>? items, bool award, JsonNode? value)
    {
        var all = items ?? [];
        var oldIds = all.Where(i => IsAward(i) == award).Select(i => i.Id).ToList();
        var fromChat = (value?.AsArray() ?? [])
            .Select((node, i) => node.Deserialize<FlowActivityDraftDto>()! with
            {
                Id = i < oldIds.Count && oldIds[i] is { } id ? id : Guid.NewGuid().ToString(),
            });
        var kept = all.Where(i => IsAward(i) != award);
        return award ? kept.Concat(fromChat).ToList() : fromChat.Concat(kept).ToList();
    }

    private static bool KeepFromPrevious(string key, JsonNode? previous)
    {
        if (previous is not JsonObject field) return false;
        var status = field["status"]?.GetValue<string>();
        if (!ValueStatuses.Contains(status)) return true;
        // Nháp chỉ có englishPlanned = false, không phân biệt "chưa hỏi" với "chưa có kế hoạch"
        return key == "english_plan" && field["value"]?["planned"]?.GetValue<bool>() == false;
    }

    private static JsonNode? ValueOf(JsonNode? field) =>
        field is JsonObject f && ValueStatuses.Contains(f["status"]?.GetValue<string>()) ? f["value"] : null;

    private static bool IsAward(FlowActivityDraftDto item) =>
        string.Equals(item.Category, "achievement", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.Category, "award", StringComparison.OrdinalIgnoreCase);

    private static JsonNode? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value);

    private static string? Str(JsonNode? value) => value?.GetValue<string>();

    private static JsonArray? ListOrNull(IEnumerable<JsonNode> nodes)
    {
        var list = nodes.ToArray();
        return list.Length == 0 ? null : new JsonArray(list);
    }
}
