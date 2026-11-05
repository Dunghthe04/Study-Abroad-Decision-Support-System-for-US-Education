using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

/// <summary>Giá trị giả định học sinh nêu trong chat, đã được advisor kiểm tra.</summary>
public record ChatScenario(SimulationOverrides Overrides, IReadOnlyList<string> Unsupported);

/// <summary>
/// Hỏi what-if trong chat: advisor trả action "simulate" kèm giá trị đã kiểm tra (ActionData),
/// ChatService chạy SimulateAsync có sẵn rồi dùng các hàm ở đây dựng dữ liệu cho Gemini viết lời,
/// câu mẫu dự phòng và dữ liệu cho panel kết quả.
/// </summary>
public static class ChatSimulation
{
    public const string Action = "simulate";
    public const string WidgetType = "simulate";

    // Như trang gợi ý: mỗi nhóm nêu tối đa 4 trường; mỗi trường đổi nhóm kèm tối đa 2 lý do
    public const int MaxPerGroup = 4;
    public const int MaxReasons = 2;
    public const int MaxOthers = 400;

    public const string Failed = "Mình chưa thử được kịch bản này, bạn thử lại sau ít phút nhé.";
    private const string SeeTable = "Bạn xem chi tiết ở bảng kết quả nhé.";

    // Nhóm hiển thị theo đúng thứ tự và tên trên trang Gợi ý trường. Nhóm "chưa đủ dữ liệu" không nêu trong chat
    private static readonly (string Key, string Title)[] Groups =
    [
        ("reach", "Khó vào"),
        ("match", "Vừa sức"),
        ("safety", "An toàn hơn"),
        ("aid", "Cần học bổng lớn"),
    ];

    // Nhóm từ khó vào nhất đến dễ vào nhất. Chưa đủ dữ liệu (NA) coi như Khó vào: biết thêm thông tin không làm trường dễ vào hơn
    private static int Rank(string status) => status switch
    {
        "OUT" => 0,
        "AID" => 1,
        "R" or "NA" => 2,
        "M" => 3,
        "S" => 4,
        _ => 2,
    };

    private static string StatusName(string status) => status switch
    {
        "R" => "Khó vào",
        "M" => "Vừa sức",
        "S" => "An toàn hơn",
        "NA" => "Chưa đủ dữ liệu",
        "AID" => "Cần học bổng lớn",
        "OUT" => "Bị loại",
        _ => status,
    };

    private static string NeedName(string need) => need switch
    {
        "mandatory" => "bắt buộc",
        "preferred" => "nên có",
        "optional" => "không cần",
        _ => need,
    };

    /// <summary>Đọc ActionData của advisor. Null nếu thiếu hoặc không có giá trị giả định nào.</summary>
    public static ChatScenario? Parse(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } d
            || !d.TryGetProperty("changes", out var c) || c.ValueKind != JsonValueKind.Object)
            return null;

        decimal? Number(string name) =>
            c.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var x) ? x : null;

        decimal? gpa = null;
        if (c.TryGetProperty("gpa", out var g) && g.ValueKind == JsonValueKind.Object
            && g.TryGetProperty("value", out var gv) && gv.ValueKind == JsonValueKind.Number && gv.TryGetDecimal(out var value))
        {
            var scale = g.TryGetProperty("scale", out var s) && s.TryGetInt32(out var sc) ? sc : 4;
            gpa = scale == 10 ? TranscriptGpa.To4(value) : value;
        }

        var need = c.TryGetProperty("scholarshipNeed", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var overrides = new SimulationOverrides(gpa, Number("sat"), Number("act"), Number("ielts"), Number("budgetUsd"), need, null,
            Number("toefl"), Number("duolingo"));
        if (overrides == new SimulationOverrides(null, null, null, null, null, null, null))
            return null;

        var unsupported = d.TryGetProperty("unsupported", out var u) && u.ValueKind == JsonValueKind.Array
            ? u.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : [];
        return new ChatScenario(overrides, unsupported);
    }

    private static string Dec(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Money(decimal v) =>
        ((long)Math.Round(v)).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");

    // "SAT 1500 (hiện tại 1290)": điều đang thử kèm giá trị hiện tại, để học sinh kiểm tra hệ thống hiểu đúng
    private static string Label(SimulationOverrides o, SimulationProfile p)
    {
        static string Now(string? text) => text is null ? "" : $" (hiện tại {text})";
        var parts = new List<string>();
        if (o.Gpa is { } gpa) parts.Add($"GPA {Dec(gpa)}{Now(p.Gpa is { } v ? Dec(v) : null)}");
        if (o.Sat is { } sat) parts.Add($"SAT {Dec(sat)}{Now(p.Sat?.ToString(CultureInfo.InvariantCulture))}");
        if (o.Act is { } act) parts.Add($"ACT {Dec(act)}{Now(p.Act?.ToString(CultureInfo.InvariantCulture))}");
        if (o.Ielts is { } ielts) parts.Add($"IELTS {Dec(ielts)}{Now(p.Ielts is { } i ? Dec(i) : null)}");
        if (o.Toefl is { } toefl) parts.Add($"TOEFL {Dec(toefl)}{Now(p.Toefl is { } t ? Dec(t) : null)}");
        if (o.Duolingo is { } duolingo) parts.Add($"Duolingo {Dec(duolingo)}{Now(p.Duolingo is { } d ? Dec(d) : null)}");
        if (o.BudgetUsd is { } budget) parts.Add($"ngân sách {Money(budget)} USD mỗi năm{Now(p.BudgetUsd is { } b ? Money(b) + " USD" : null)}");
        if (o.ScholarshipNeed is { } need)
            parts.Add($"mức cần học bổng {NeedName(need)}{Now(p.ScholarshipNeed is { } pn ? NeedName(pn) : null)}");
        return string.Join(", ", parts);
    }

    // Yếu tố đang thử, theo khóa advisor dùng để giải thích tác động (GPA, SAT làm đổi nhóm; IELTS, TOEFL, Duolingo chỉ làm trường vào hoặc ra khỏi danh sách)
    private static List<string> Factors(SimulationOverrides o)
    {
        var factors = new List<string>();
        if (o.Gpa is not null) factors.Add("gpa");
        if (o.Sat is not null) factors.Add("sat");
        if (o.Act is not null) factors.Add("act");
        if (o.Ielts is not null) factors.Add("ielts");
        if (o.Toefl is not null) factors.Add("toefl");
        if (o.Duolingo is not null) factors.Add("duolingo");
        if (o.BudgetUsd is not null) factors.Add("budget");
        if (o.ScholarshipNeed is not null) factors.Add("scholarship");
        return factors;
    }

    /// <summary>
    /// Dữ liệu cho Gemini viết lời: danh sách trường theo nhóm của kết quả mô phỏng (mỗi nhóm tối đa 4 trường, thứ tự như trang gợi ý),
    /// trường nào vừa đổi nhóm so với hồ sơ thật thì kèm nhóm cũ và lý do. Không trường nào đổi nhóm thì không kèm danh sách.
    /// </summary>
    public static ScenarioNoteRequestDto Note(SimulationResult result, ChatScenario scenario)
    {
        var better = result.Changes.Any(c => Rank(c.To) > Rank(c.From));
        var worse = result.Changes.Any(c => Rank(c.To) < Rank(c.From));
        var direction = better && worse ? "mixed" : better ? "better" : worse ? "worse" : "none";

        var items = JsonNode.Parse(result.ItemsJson) as JsonObject;
        var names = (items?["scored"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Where(s => s["code"] is not null && s["name"] is not null)
            .GroupBy(s => s["code"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()["name"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
        var moved = result.Changes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<ScenarioGroupDto>();
        if (direction != "none")
        {
            foreach (var (key, title) in Groups)
            {
                var entries = (items?[key] as JsonArray ?? []).OfType<JsonObject>().Where(e => e["code"] is not null).ToList();
                if (entries.Count == 0)
                    continue;
                var schools = entries.Take(MaxPerGroup).Select(entry =>
                {
                    var code = entry["code"]!.GetValue<string>();
                    listed.Add(code);
                    moved.TryGetValue(code, out var change);
                    var reasons = change is null
                        ? []
                        : (entry["why"] as JsonArray ?? []).Select(w => w?.GetValue<string>()).OfType<string>().Take(MaxReasons).ToList();
                    return new ScenarioSchoolDto(
                        names.GetValueOrDefault(code, code), change is null ? null : StatusName(change.From), reasons, key == "aid");
                }).ToList();
                groups.Add(new ScenarioGroupDto(key, title, schools, Math.Max(0, entries.Count - MaxPerGroup)));
            }
        }

        var others = names.Where(kv => !listed.Contains(kv.Key)).Select(kv => kv.Value).Distinct().Take(MaxOthers).ToList();
        var o = scenario.Overrides;
        var budget = o.BudgetUsd ?? result.Profile.BudgetUsd;
        var need = o.ScholarshipNeed ?? result.Profile.ScholarshipNeed;
        var student = new ScenarioStudentDto(budget is { } b ? (int)Math.Round(b) : null, need is null ? null : NeedName(need));

        return new ScenarioNoteRequestDto(
            Label(o, result.Profile), Factors(o), scenario.Unsupported, direction, student, groups, others);
    }

    /// <summary>Câu trả lời bằng code khi Gemini lỗi hoặc lời không qua kiểm tra. Cùng dữ liệu, chữ khô hơn.</summary>
    public static string Fallback(ScenarioNoteRequestDto note)
    {
        var lines = new List<string>();
        if (note.Groups.Count == 0)
        {
            lines.Add($"Với {note.Label}, danh sách trường không đổi nhóm. {SeeTable}");
        }
        else
        {
            lines.Add($"Với {note.Label} thì mình gợi ý như sau:");
            foreach (var group in note.Groups)
                lines.Add($"- {group.Title}: {string.Join(", ", group.Schools.Select(s => s.Name))}"
                          + (group.More > 0 ? $" và {group.More} trường khác" : ""));
            lines.Add(SeeTable);
        }
        if (note.Unsupported.Count > 0)
            lines.Add($"Mình chưa thử được: {string.Join(", ", note.Unsupported)}.");
        return string.Join("\n", lines);
    }

    /// <summary>Công cụ cho panel kết quả: giá trị giả định để thanh kéo điền sẵn (chỉ các giá trị học sinh nêu).</summary>
    public static Widget WidgetOf(ChatScenario scenario)
    {
        var o = scenario.Overrides;
        var values = new JsonObject();
        if (o.Gpa is { } gpa) values["gpa"] = gpa;
        if (o.Sat is { } sat) values["sat"] = sat;
        if (o.Act is { } act) values["act"] = act;
        if (o.Ielts is { } ielts) values["ielts"] = ielts;
        if (o.Toefl is { } toefl) values["toefl"] = toefl;
        if (o.Duolingo is { } duolingo) values["duolingo"] = duolingo;
        if (o.BudgetUsd is { } budget) values["budgetUsd"] = budget;
        if (o.ScholarshipNeed is { } need) values["scholarshipNeed"] = need;
        return new Widget(WidgetType, JsonSerializer.SerializeToElement(values));
    }
}
