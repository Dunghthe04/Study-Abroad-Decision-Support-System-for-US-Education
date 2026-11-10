using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

/// <summary>Giá trị giả định học sinh nêu trong chat, đã được advisor kiểm tra.</summary>
public record ChatScenario(SimulationOverrides Overrides, IReadOnlyList<string> Unsupported, ChatPriorities? Priorities = null)
{
    /// <summary>Có giá trị hồ sơ giả định (GPA, SAT...) hay chỉ đổi ưu tiên.</summary>
    public bool HasProfileValues => Overrides != new SimulationOverrides(null, null, null, null, null, null, null);
}

/// <summary>
/// Ưu tiên học sinh nêu trong chat (0..4), chỉ gồm yếu tố họ nhắc tới; yếu tố khác giữ mức hiện tại.
/// Khớp ba thanh "Ưu tiên của bạn" của trang gợi ý: chi phí thấp, học bổng nhiều, danh tiếng.
/// </summary>
public record ChatPriorities(int? Cost, int? Aid, int? Prestige);

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

    /// <summary>
    /// Đọc ActionData của advisor. Null nếu thiếu hoặc không có giá trị giả định nào.
    /// gradeScale là bảng grade_scale.10 để quy đổi GPA thang 10; null thì bảng mặc định.
    /// </summary>
    public static ChatScenario? Parse(JsonElement? data, GradeScaleConfigDto? gradeScale = null)
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
            gpa = scale == 10 ? TranscriptGpa.To4(value, gradeScale) : value;
        }

        var need = c.TryGetProperty("scholarshipNeed", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var overrides = new SimulationOverrides(gpa, Number("sat"), Number("act"), Number("ielts"), Number("budgetUsd"), need, null,
            Number("toefl"), Number("duolingo"));
        var priorities = ParsePriorities(d);
        if (overrides == new SimulationOverrides(null, null, null, null, null, null, null) && priorities is null)
            return null;

        var unsupported = d.TryGetProperty("unsupported", out var u) && u.ValueKind == JsonValueKind.Array
            ? u.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : [];
        return new ChatScenario(overrides, unsupported, priorities);
    }

    // Mỗi mức phải là số nguyên 0..4 (khoảng của thanh kéo); mức sai thì bỏ, không có mức nào hợp lệ thì coi như không đổi ưu tiên
    private static ChatPriorities? ParsePriorities(JsonElement data)
    {
        if (!data.TryGetProperty("priorities", out var p) || p.ValueKind != JsonValueKind.Object)
            return null;
        int? Level(string name) =>
            p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var x) && x is >= 0 and <= 4 ? x : null;
        var result = new ChatPriorities(Level("cost"), Level("aid"), Level("prestige"));
        return result == new ChatPriorities(null, null, null) ? null : result;
    }

    // ---- ưu tiên (ba thanh của trang gợi ý), thứ tự: chi phí thấp, học bổng nhiều, danh tiếng

    private static readonly string[] PriorityNames = ["chi phí thấp", "học bổng nhiều", "danh tiếng"];
    private static readonly string[] GroupKeys = ["reach", "match", "safety", "aid"];

    /// <summary>Ưu tiên của lần gợi ý đã lưu (criteria.pref). Không đọc được thì cân bằng 2-2-2 như trang gợi ý.</summary>
    public static IReadOnlyList<int> BasePref(string? criteriaJson)
    {
        try
        {
            if (criteriaJson is not null && JsonNode.Parse(criteriaJson) is JsonObject o && o["pref"] is JsonArray a && a.Count == 3)
            {
                var list = a.Select(n => n?.GetValue<int>() ?? -1).ToList();
                if (list.All(n => n is >= 0 and <= 4))
                    return list;
            }
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            // criteria hỏng: dùng mức cân bằng
        }
        return [2, 2, 2];
    }

    /// <summary>Ưu tiên mới: yếu tố học sinh nhắc tới lấy mức mới, còn lại giữ mức hiện tại.</summary>
    public static IReadOnlyList<int> MergePref(IReadOnlyList<int> current, ChatPriorities p) =>
        [p.Cost ?? current[0], p.Aid ?? current[1], p.Prestige ?? current[2]];

    public const int MaxMoves = 5;

    /// <summary>
    /// Đoạn trả lời về ưu tiên, do code dựng (không để AI nói số). Nói rõ ưu tiên chỉ đổi thứ tự trong nhóm,
    /// rồi nêu các trường đổi vị trí nhiều nhất giữa hai danh sách tính cùng hồ sơ nhưng khác ưu tiên (before và after).
    /// </summary>
    public static string PriorityNote(
        IReadOnlyList<int> before, IReadOnlyList<int> after, ChatPriorities asked, string beforeItemsJson, string afterItemsJson)
    {
        bool[] mentioned = [asked.Cost is not null, asked.Aid is not null, asked.Prestige is not null];
        var levels = Enumerable.Range(0, 3).Where(i => mentioned[i]).Select(i =>
            before[i] == after[i]
                ? $"{PriorityNames[i]} {after[i]}/4 (như hiện tại)"
                : $"{PriorityNames[i]} {after[i]}/4 (trước là {before[i]}/4)");
        var lines = new List<string>
        {
            $"Ưu tiên mới: {string.Join(", ", levels)}. Ưu tiên chỉ đổi thứ tự các trường trong cùng một nhóm, không đổi nhóm của trường nào."
        };

        var afterItems = JsonNode.Parse(afterItemsJson) as JsonObject;
        var moves = Moves(JsonNode.Parse(beforeItemsJson) as JsonObject, afterItems);
        if (moves.Count == 0)
        {
            lines.Add(before.SequenceEqual(after)
                ? "Mức này giống hiện tại nên thứ tự các trường không đổi."
                : "Với ưu tiên này, thứ tự các trường trong mỗi nhóm gần như không đổi.");
            return string.Join("\n", lines);
        }

        // Yếu tố đổi nhiều nhất quyết định nói con số nào của trường (chi phí, điểm học bổng hoặc điểm danh tiếng)
        var main = Enumerable.Range(0, 3).OrderByDescending(i => Math.Abs(after[i] - before[i])).First();
        var scored = (afterItems?["scored"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(s => s["code"] is not null)
            .GroupBy(s => s["code"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        lines.Add("Các trường đổi vị trí nhiều nhất trong nhóm của mình:");
        foreach (var m in moves.Take(MaxMoves))
        {
            var fact = scored.TryGetValue(m.Code, out var s) ? PriorityFact(main, s) : null;
            var direction = m.To < m.From ? "lên" : "xuống";
            lines.Add($"- {m.Name} ({m.GroupTitle}): từ vị trí {m.From} {direction} vị trí {m.To}" + (fact is null ? "" : $", {fact}"));
        }
        if (moves.Count > MaxMoves)
            lines.Add($"Và {moves.Count - MaxMoves} trường khác cũng đổi vị trí. {SeeTable}");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Nút bước tiếp theo sau một kết quả what-if, do code chọn từ chính kết quả (tối đa 2):
    /// hiểu vì sao trường thay đổi nhiều nhất đổi nhóm (hoặc đổi vị trí), rồi xem cần gì để vào một trường Khó vào.
    /// Nút thứ hai KHÔNG chọn sẵn trường (học sinh chưa nói quan tâm trường nào): mở ô chọn trường để họ tự chọn.
    /// Trường bị loại khỏi danh sách không có để giải thích nên không được đưa ra.
    /// </summary>
    public static IReadOnlyList<QuickReply> NextSteps(SimulationResult result, string? beforeItemsJson = null)
    {
        var steps = new List<QuickReply>();

        var change = result.Changes
            .Where(c => c.To != "OUT" && Rank(c.To) != Rank(c.From))
            .OrderByDescending(c => Math.Abs(Rank(c.To) - Rank(c.From)))
            .FirstOrDefault();
        if (change is not null)
        {
            var up = Rank(change.To) > Rank(change.From);
            steps.Add(ExplainStep(change.Code, change.Name, $"Vì sao {change.Name} {(up ? "lên" : "xuống")} {StatusName(change.To)}?"));
        }
        else if (beforeItemsJson is not null)
        {
            var move = Moves(JsonNode.Parse(beforeItemsJson) as JsonObject, JsonNode.Parse(result.ItemsJson) as JsonObject).FirstOrDefault();
            if (move is not null)
            {
                steps.Add(ExplainStep(move.Code, move.Name, $"Vì sao {move.Name} {(move.To < move.From ? "lên" : "xuống")} vị trí {move.To}?"));
            }
        }

        // Có trường Khó vào trong danh sách thì mới có "mục tiêu" để hỏi cần gì
        var items = JsonNode.Parse(result.ItemsJson) as JsonObject;
        if ((items?["reach"] as JsonArray)?.Count > 0)
        {
            const string text = "Cần gì để vào trường em muốn?";
            steps.Add(new QuickReply(text, text, new ChatAction("goal_seek")));
        }
        return steps;
    }

    private static QuickReply ExplainStep(string code, string name, string label) =>
        new(label, $"Vì sao {name} xếp ở nhóm này?", new ChatAction("explain", code));

    private static string? PriorityFact(int factor, JsonObject school)
    {
        double? Num(string name) => school[name] is JsonValue v && v.TryGetValue<double>(out var x) ? x : null;
        return factor switch
        {
            0 => Num("cost") is { } c ? $"chi phí {Money((decimal)c)} USD mỗi năm" : null,
            // Nói bằng USD thật, không nói điểm: số trung bình là của người được nhận học bổng, không hứa bạn sẽ nhận được
            1 => school["aidPolicy"]?.GetValue<string>() == "none"
                ? "trường không có học bổng cho du học sinh"
                : Num("aidAvg") is { } aid && Num("h") is not null
                    ? $"người được học bổng nhận trung bình {Money((decimal)aid)} USD mỗi năm"
                      + (Num("cost") is { } total and > 0 ? $" (khoảng {Math.Round(aid / total * 100)}% chi phí)" : "")
                    : "trường chưa có số liệu học bổng",
            _ => Num("q") is { } q ? $"chỉ số chất lượng và xếp hạng của trường {Math.Round(q * 100)}/100" : null,
        };
    }

    private record Move(string Code, string Name, string GroupTitle, int From, int To);

    // Trường nằm cùng nhóm ở hai danh sách nhưng khác vị trí, nhiều bước nhất trước
    private static List<Move> Moves(JsonObject? before, JsonObject? after)
    {
        var names = (after?["scored"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(s => s["code"] is not null && s["name"] is not null)
            .GroupBy(s => s["code"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()["name"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
        var moves = new List<Move>();
        for (var g = 0; g < GroupKeys.Length; g++)
        {
            var key = GroupKeys[g];
            List<string> Codes(JsonObject? items) => (items?[key] as JsonArray ?? []).OfType<JsonObject>()
                .Select(e => e["code"]?.GetValue<string>()).OfType<string>().ToList();
            var was = Codes(before);
            var now = Codes(after);
            for (var i = 0; i < now.Count; i++)
            {
                var from = was.FindIndex(c => string.Equals(c, now[i], StringComparison.OrdinalIgnoreCase));
                if (from >= 0 && from != i)
                    moves.Add(new Move(now[i], names.GetValueOrDefault(now[i], now[i]), Groups[g].Title, from + 1, i + 1));
            }
        }
        return moves.OrderByDescending(m => Math.Abs(m.From - m.To)).ThenBy(m => m.To).ToList();
    }

    private static string Dec(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Money(decimal v) =>
        ((long)Math.Round(v)).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");

    /// <summary>Mô tả ngắn các giá trị giả định ("GPA 3.6, SAT 1500"), không kèm giá trị hiện tại. Null nếu không có giá trị nào.</summary>
    public static string? Describe(SimulationOverrides? o)
    {
        if (o is null) return null;
        var parts = new List<string>();
        if (o.Gpa is { } gpa) parts.Add($"GPA {Dec(gpa)}");
        if (o.Sat is { } sat) parts.Add($"SAT {Dec(sat)}");
        if (o.Act is { } act) parts.Add($"ACT {Dec(act)}");
        if (o.Ielts is { } ielts) parts.Add($"IELTS {Dec(ielts)}");
        if (o.Toefl is { } toefl) parts.Add($"TOEFL {Dec(toefl)}");
        if (o.Duolingo is { } duolingo) parts.Add($"Duolingo {Dec(duolingo)}");
        if (o.BudgetUsd is { } budget) parts.Add($"ngân sách {Money(budget)} USD mỗi năm");
        if (o.ScholarshipNeed is { } need) parts.Add($"mức cần học bổng {NeedName(need)}");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

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
    public static Widget WidgetOf(ChatScenario scenario, IReadOnlyList<int>? pref = null)
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
        if (pref is { Count: 3 }) values["pref"] = new JsonArray(pref[0], pref[1], pref[2]);   // thanh ưu tiên trên trang nhúc nhích theo
        return new Widget(WidgetType, JsonSerializer.SerializeToElement(values));
    }
}
