using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

/// <summary>Điều học sinh hỏi trong chat: tên trường (chữ thô SLM đã trích) và nhóm đích (reach | match | safety | null).</summary>
// Code: có khi nút trên giao diện đã biết mã trường (hành động có cấu trúc), khi đó không cần khớp tên
public record ChatGoalQuery(string School, string? Target, string? Code = null);

/// <summary>
/// "Cần gì để vào trường X" trong chat: advisor trả action "goal_seek" kèm tên trường chữ thô (ActionData.school) và nhóm đích.
/// ChatService khớp tên với danh sách trường: chắc chắn thì chạy goal-seeking, mơ hồ thì hỏi "Ý bạn là trường nào?" kèm nút bấm,
/// không có thì báo. Các hàm ở đây dựng câu hỏi lại, câu tóm tắt và công cụ mở khung kết quả trên trang gợi ý.
/// </summary>
public static class ChatGoalSeek
{
    public const string Action = "goal_seek";
    public const string WidgetType = "goal_seek";
    public const string PickerType = "school";

    public const string Failed = "Mình chưa tính được phần này, bạn thử lại sau ít phút nhé.";
    public const string AskSchool = "Bạn muốn vào trường nào? Chọn hoặc gõ tên trường ở ô bên dưới nhé.";
    public const string AskExplainSchool = "Bạn muốn xem giải thích về trường nào? Chọn hoặc gõ tên trường ở ô bên dưới nhé.";
    public const int MaxOthers = 400;

    /// <summary>Đọc ActionData của advisor. Null nếu thiếu tên trường.</summary>
    public static ChatGoalQuery? Parse(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } d)
            return null;
        var target = d.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        // Nút trên giao diện gửi thẳng mã trường: không cần khớp tên
        if (d.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(c.GetString()))
            return new ChatGoalQuery("", target, c.GetString()!.Trim());
        if (!d.TryGetProperty("school", out var s) || s.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(s.GetString()))
            return null;
        return new ChatGoalQuery(s.GetString()!.Trim(), target);
    }

    // ---------------------------------------------------------------- không chắc là trường nào

    /// <summary>Không thấy trường nào khớp: nói rõ để học sinh gõ lại hoặc chọn từ danh sách.</summary>
    public static string NotFound(string text) =>
        $"Mình chưa thấy trường “{text}” trong danh sách của hệ thống. Bạn tìm trong ô bên dưới nhé.";

    /// <summary>Ô chọn trường có gợi ý (gõ đến đâu hiện đến đó). Data.target là nhóm đích học sinh đã nói, để câu hỏi gửi lại giữ nguyên.</summary>
    // kind: việc làm sau khi chọn trường. Không có là "cần gì để vào", "explain" là giải thích trường (frontend đổi câu gửi lại)
    // codes: chỉ cho chọn trong các trường này (null là tất cả trường)
    // suggested: các trường hiện sẵn dạng nút bấm khi học sinh chưa gõ gì (vd đầu danh sách gợi ý)
    public static Widget SchoolPicker(
        string? target, string? kind = null, IReadOnlyList<string>? codes = null, IReadOnlyList<string>? suggested = null) =>
        new(PickerType, JsonSerializer.SerializeToElement(new JsonObject
        {
            ["target"] = target,
            ["kind"] = kind,
            ["codes"] = Codes(codes),
            ["suggested"] = Codes(suggested),
        }));

    private static JsonArray? Codes(IReadOnlyList<string>? codes) =>
        codes is null ? null : new JsonArray(codes.Select(c => (JsonNode?)JsonValue.Create(c)).ToArray());

    /// <summary>Gõ tắt hoặc mơ hồ: hỏi "Ý bạn là trường nào?" và đưa các trường có thể đúng.</summary>
    public static string Clarify(string text, int count) =>
        count == 1 ? $"Ý bạn là trường này phải không?" : $"“{text}” có thể là một trong các trường sau, ý bạn là trường nào?";

    /// <summary>
    /// Nút chọn trường. Mỗi nút gửi lại cả câu hỏi đầy đủ ("Cần gì để vào Boston University ở nhóm Vừa sức?"),
    /// nên không cần nhớ trạng thái "đang chờ chọn trường": câu mới đi qua SLM như mọi câu và khớp chính xác.
    /// </summary>
    public static IReadOnlyList<QuickReply> Choices(IReadOnlyList<SchoolRef> candidates, string? target) =>
        candidates.Select(c => new QuickReply(c.Name, Question(c.Name, target))).ToList();

    public static string Question(string schoolName, string? target) =>
        target is null ? $"Cần gì để vào {schoolName}?" : $"Cần gì để vào {schoolName} ở nhóm {TargetName(target)}?";

    private static string TargetName(string target) => target switch
    {
        "reach" => "Khó vào",
        "match" => "Vừa sức",
        "safety" => "An toàn hơn",
        _ => target,
    };

    // ---------------------------------------------------------------- kết quả

    private static string GroupName(string code) => code switch
    {
        "R" => "Khó vào",
        "M" => "Vừa sức",
        "S" => "An toàn hơn",
        _ => "Chưa đủ dữ liệu",
    };

    private static string Money(decimal value) =>
        ((long)Math.Round(value)).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");

    private static string Number(string factor, decimal? value) =>
        value is null ? "chưa có" : factor == "gpa" ? value.Value.ToString("0.##", CultureInfo.GetCultureInfo("vi-VN")) : ((int)Math.Round(value.Value)).ToString(CultureInfo.InvariantCulture);

    private static string FactorName(string factor) => factor switch
    {
        "sat" => "SAT",
        "gpa" => "GPA",
        _ => "bậc ngoại khóa",
    };

    private static string Difficulty(string value) => value switch
    {
        "easy" => "khả thi",
        "hard" => "khó",
        _ => "rất khó",
    };

    // Mã lý do xếp nhóm (GroupReasons) → vì sao không đạt nhóm đích
    // Có số liệu (stats) thì nói rõ con số và mốc: "chỉ nhận 9% hồ sơ, dưới mốc 15%", không chỉ "ít hơn hẳn"
    private static string Pct(decimal fraction) =>
        (fraction * 100).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN")) + "%";

    private static string Reason(string code, GoalSchoolStats? stats = null) => code switch
    {
        GroupReasons.LowAcceptRate => stats?.AcceptRate is { } rate
            ? $"trường chỉ nhận {Pct(rate)} hồ sơ, dưới mốc {Pct((decimal)stats.AlwaysReachRate)}, nên luôn xếp Khó vào, điểm cao đến đâu cũng không đổi nhóm"
            : "trường chỉ nhận dưới 15% hồ sơ nên luôn xếp Khó vào, điểm cao đến đâu cũng không đổi nhóm",
        GroupReasons.CappedSelective => stats?.AcceptRate is { } rate2
            ? $"trường nhận {Pct(rate2)} hồ sơ, dưới mốc {Pct((decimal)stats.SafetyMinRate)}, nên chỉ tối đa là Vừa sức"
            : "trường nhận dưới 50% hồ sơ nên chỉ tối đa là Vừa sức",
        GroupReasons.CappedWeakMetric => "một chỉ số của bạn còn thấp hơn mốc 25% của trường nên chưa tính là An toàn",
        GroupReasons.IntlDowngrade => stats is { AcceptRate: { } all, IntlAcceptRate: { } intl, IntlRatio: { } ratio }
            ? $"trường nhận du học sinh {Pct(intl)} trong khi tỷ lệ nhận chung là {Pct(all)}, chỉ bằng {Pct((decimal)ratio)} mức chung và dưới mốc {Pct((decimal)stats.IntlRatioCut)}, nên bị hạ một nhóm"
            : "trường nhận du học sinh ít hơn hẳn tỷ lệ chung nên bị hạ một nhóm",
        GroupReasons.NeedTest => "trường bắt buộc nộp điểm SAT hoặc ACT mà hồ sơ của bạn chưa có",
        GroupReasons.NoAcademicData => "trường chưa có đủ số liệu học thuật để so sánh",
        _ => "",
    };

    private static string Describe(GoalPlanDto plan) =>
        string.Join(" và ", plan.Changes.Select(c => $"{FactorName(c.Factor)} {Number(c.Factor, c.From)} → {Number(c.Factor, c.To)}"))
        + $" ({Difficulty(plan.Difficulty)})";

    // Tài chính đứng trước học thuật. Luôn nói rõ ngân sách của học sinh là bao nhiêu TRƯỚC khi nói vượt: nhất là khi đang xem giả định,
    // học sinh dễ quên ngân sách gốc. "Vượt" tính so với trần = ngân sách cộng dung sai (như bộ lọc của lượt gợi ý), nên nêu cả trần
    private static string Finance(GoalBudgetDto b, bool simulated = false)
    {
        var cost = b.CostUsd is { } c ? $"{Money(c)} USD mỗi năm" : null;
        var who = simulated ? "Ngân sách giả định của bạn" : "Ngân sách của bạn";
        var budget = b.BudgetUsd is { } bu ? $"{who} là {Money(bu)} USD mỗi năm" : null;
        var ceiling = b.BudgetUsd is { } cb
            ? $"hệ thống cho phép vượt tới {Money(cb * (1 + RecommendationService.BudgetTolerance))} USD, dung sai {(int)(RecommendationService.BudgetTolerance * 100)}%"
            : null;
        return b.Status switch
        {
            "ok" => $"{budget}. Chi phí khoảng {cost}, nằm trong ngân sách.",
            "need_aid" => $"{budget} ({ceiling}). Chi phí khoảng {cost}, vượt mức đó khoảng {Money(b.ShortfallUsd ?? 0)} USD: cần ngân sách từ {Money(b.MinBudgetUsd ?? 0)} USD mỗi năm hoặc học bổng lớn.",
            "over_budget" => $"{budget} ({ceiling}). Chi phí khoảng {cost}, vượt mức đó và trường không có học bổng phù hợp nên không nằm trong danh sách của bạn. Cần ngân sách từ {Money(b.MinBudgetUsd ?? 0)} USD mỗi năm.",
            "no_budget" => $"Chi phí khoảng {cost}. Hồ sơ của bạn không đặt trần ngân sách mỗi năm.",
            _ => "Trường chưa có số liệu chi phí.",
        };
    }

    /// <summary>
    /// Lời giải thích dưới bảng "bạn so với trường", ghép bằng code từ kết quả đã tính nên số luôn khớp với bảng và danh sách gợi ý.
    /// Năm phần: bạn đang ở nhóm nào; vì sao không lên được nhóm đích (nếu bị chặn); bạn đang đứng ở đâu so với trường; cần cải thiện gì
    /// và nên nộp trường này theo vai trò nào. Đây là lời dự phòng khi Claude không viết được, đồng thời là dữ kiện gửi Claude diễn đạt lại.
    /// Câu đầu nói lại tên trường đã hiểu để học sinh sửa nếu nhầm.
    /// </summary>
    public static string Summarize(SchoolGoalResult r) => string.Join("\n", Body(r));

    // Vì sao trường chưa nằm trong danh sách gợi ý của học sinh (bộ lọc cứng)
    private static string ExcludeText(string reason) => reason switch
    {
        "BUDGET" => "chi phí vượt ngân sách của bạn",
        "ENGLISH" => "điểm tiếng Anh của bạn chưa đủ",
        "ENGLISH_NO_CERT" => "bạn chưa có chứng chỉ tiếng Anh",
        "MAJOR" => "trường không có ngành bạn chọn",
        "STATE" => "trường không nằm ở bang bạn chọn",
        "CONTROL" => "loại trường (công lập hoặc tư thục) không đúng điều bạn chọn",
        _ => "chưa qua bộ lọc của bạn",
    };

    private static string Gpa(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"));

    private static string Cap(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static bool IsExcluded(SchoolGoalResult r) => r.Position?.Status == "excluded" || r.FilterReason is not null;

    // Câu in đậm đầu tiên: kết luận trường đang ở đâu với học sinh
    private static string Headline(SchoolGoalResult r)
    {
        if (r.Current == "NA")
            return $"Hệ thống chưa đủ số liệu để xếp {r.Name} vào nhóm nào với hồ sơ của bạn.";
        if (IsExcluded(r))
        {
            var reason = r.Position?.ExcludeReason ?? r.FilterReason ?? "";
            return $"{r.Name} chưa nằm trong danh sách gợi ý của bạn vì {ExcludeText(reason)}. Nếu đủ điều kiện đó, trường sẽ ở nhóm {GroupName(r.Current)} với bạn.";
        }
        return r.Outcome switch
        {
            "already_reached" =>
                $"Với hồ sơ hiện tại, {r.Name} đã ở nhóm {GroupName(r.Current)} với bạn, bạn không cần thay đổi gì để đạt nhóm {GroupName(r.Target)}.",
            "blocked" when r.Best == r.Current =>
                $"Với hồ sơ hiện tại, {r.Name} đang ở nhóm {GroupName(r.Current)} với bạn, và đây là nhóm cao nhất trường này có thể có.",
            "blocked" =>
                $"Hiện tại {r.Name} đang ở nhóm {GroupName(r.Current)} với bạn; nhóm cao nhất bạn có thể đạt ở trường này là {GroupName(r.Best)}.",
            "reachable" =>
                $"Hiện tại {r.Name} đang ở nhóm {GroupName(r.Current)} với bạn. Bạn có thể lên nhóm {GroupName(r.Target)} nếu cải thiện hồ sơ.",
            _ => $"Hiện tại {r.Name} đang ở nhóm {GroupName(r.Current)} với bạn.",
        };
    }

    // Lý do xếp nhóm mà học sinh không thay đổi được (luật của trường áp cho mọi hồ sơ) hay có thể thay đổi
    private static bool IsSchoolRule(string code) =>
        code is GroupReasons.LowAcceptRate or GroupReasons.CappedSelective or GroupReasons.IntlDowngrade;

    // "Vì sao không lên được nhóm X": các lý do xếp nhóm của nhóm đích, nói thẳng điều nào là luật của trường (không phải do hồ sơ yếu)
    private static List<string> WhyNot(SchoolGoalResult r)
    {
        var reasons = r.Reasons.Where(c => Reason(c, r.Stats) != "").ToList();
        if (r.Outcome != "blocked" || r.Current == "NA" || reasons.Count == 0) return [];
        var lines = new List<string> { $"Vì sao không lên được nhóm {GroupName(r.Target)}:" };
        foreach (var code in reasons)
        {
            var text = Cap(Reason(code, r.Stats));
            lines.Add(IsSchoolRule(code)
                ? $"- {text}. Đây là luật áp cho mọi hồ sơ, không phải do hồ sơ của bạn yếu và bạn không thể thay đổi."
                : $"- {text}.");
        }
        return lines;
    }

    private static bool SamePlan(GoalPlanDto a, GoalPlanDto b) =>
        a.Changes.Count == b.Changes.Count && a.Changes.Zip(b.Changes).All(x => x.First.Factor == x.Second.Factor && x.First.To == x.Second.To);

    private static string PositionPart(GoalCompareRow row) =>
        row.Position is { } pos ? $"hơn khoảng {Math.Clamp((int)Math.Round(pos * 100), 0, 100)}% sinh viên của trường" : "";

    // "Bạn đang ở đâu": từng tiêu chí của bảng nói thành lời, kèm ý nghĩa (nằm giữa, thấp hơn, cao hơn)
    private static List<string> WhereYouAre(SchoolGoalResult r)
    {
        var items = new List<string>();
        foreach (var row in r.Comparison ?? [])
        {
            switch (row.Factor)
            {
                case "sat" or "act" when row.Student is { } score && row.Low is { } low && row.High is { } high:
                    var test = row.Factor == "act" ? "ACT" : "SAT";
                    var where = row.Status switch
                    {
                        "below" => $"thấp hơn mốc dưới {Math.Abs((int)(row.Gap ?? 0))} điểm",
                        "above" => "cao hơn mốc trên của trường",
                        _ => "nằm trong khoảng của trường",
                    };
                    items.Add($"{test} {(int)score}: {Join(PositionPart(row), $"khoảng của trường {(int)low}–{(int)high}")}, {where}");
                    break;
                case "gpa" when row.Student is { } gpa && row.Low is { } avg:
                    var level = row.Status switch
                    {
                        "below" => $"thấp hơn trung bình {Gpa(Math.Abs(row.Gap ?? 0))}",
                        "above" => $"cao hơn trung bình {Gpa(Math.Abs(row.Gap ?? 0))}",
                        _ => "ngang mức trung bình",
                    };
                    items.Add($"GPA {Gpa(gpa)}: {Join(PositionPart(row), $"trung bình của trường {Gpa(avg)}")}, {level}");
                    break;
                case "ec" when row.Status == "below":
                    items.Add($"Ngoại khóa bậc {(int)(row.Student ?? 0)}/5: thấp hơn mức trung bình (bậc 3), kéo nhẹ điểm vị trí của bạn xuống");
                    break;
                case "ec" when row.Status == "above":
                    items.Add($"Ngoại khóa bậc {(int)(row.Student ?? 0)}/5: cao hơn mức trung bình (bậc 3), cộng thêm vào điểm vị trí của bạn");
                    break;
                case "accept" when row.Status == "downgrade" && row is { Low: { } rate, High: { } intl, Gap: { } ratio }:
                    items.Add($"Tỷ lệ nhận du học sinh {Pct(intl)} (chung {Pct(rate)}), chỉ bằng {Pct(ratio)} mức chung, dưới một nửa nên hệ thống hạ trường xuống một nhóm. Đây là luật áp cho mọi hồ sơ, bạn không thể thay đổi");
                    break;
            }
        }
        if (r.Budget.Status == "ok" && r.Budget.CostUsd is { } cost)
            items.Add($"Chi phí khoảng {Money(cost)} USD mỗi năm: nằm trong ngân sách của bạn");
        else if (r.Budget.Status is "need_aid" or "over_budget")
            items.Add("Chi phí vượt ngân sách của bạn (chi tiết ở dưới)");

        return items.Count == 0 ? [] : ["Bạn đang ở đâu so với trường:", .. items.Select(i => $"- {i}")];
    }

    private static string Join(string first, string second) => first.Length == 0 ? second : $"{first} ({second})";

    // Cải thiện trong cùng nhóm: nhóm không đổi nhưng vị trí của học sinh so với sinh viên của trường tốt hơn
    private static List<string> StrengthenWithinGroup(SchoolGoalResult r)
    {
        var items = new List<string>();
        foreach (var row in r.Comparison ?? [])
        {
            switch (row.Factor)
            {
                case "sat" or "act" when (row.Status is "below" or "within") && row.Student is { } score && row.High is { } high && score < high:
                    var test = row.Factor == "act" ? "ACT" : "SAT";
                    items.Add($"Đưa {test} lên {(int)high} (mốc trên của trường): bạn sẽ hơn khoảng 75% sinh viên của trường" +
                              (row.Position is { } p ? $", hiện là khoảng {Math.Clamp((int)Math.Round(p * 100), 0, 100)}%." : "."));
                    break;
                case "gpa" when row.Status == "below" && row.Low is { } avg:
                    items.Add($"Nâng GPA lên mức trung bình của trường ({Gpa(avg)}).");
                    break;
                case "ec" when row.Status == "below":
                    items.Add($"Nâng ngoại khóa từ bậc {(int)(row.Student ?? 0)} lên bậc 3 (mức trung bình) để không còn bị trừ điểm vị trí.");
                    break;
            }
        }
        return items;
    }

    // Phần "cần làm gì". Mức nhảy lên GPA 4,0 được nói thẳng là mức tối đa của thang điểm, không để học sinh tưởng còn khả thi
    private static List<string> HowToImprove(SchoolGoalResult r)
    {
        var lines = new List<string>();
        var target = GroupName(r.Target);
        switch (r.Outcome)
        {
            case "reachable":
                lines.Add($"Để lên nhóm {target}:");
                if (r.Alone.Count > 0)
                {
                    lines.Add("Mỗi cách sau đều đủ nếu bạn chỉ thay đổi một thứ:");
                    lines.AddRange(r.Alone.Select(p => "- " + Describe(p)));
                }
                else
                {
                    lines.Add("Thay đổi riêng một thứ chưa đủ, bạn cần cải thiện nhiều thứ cùng lúc.");
                }
                if (r.Cheapest is { Changes.Count: > 1 } combined)
                    lines.Add("Cách kết hợp ít công sức nhất: " + Describe(combined));
                // Chỉ khi mức nên đặt thật sự cao hơn mức tối thiểu (trùng nhau thì không có gì để "cao hơn", vd GPA đã chạm 4,0)
                if (r.Recommended is { } recommended && !r.Alone.Concat([r.Cheapest]).Any(p => p is not null && SamePlan(p, recommended)))
                    lines.Add("Nên đặt mục tiêu cao hơn mức tối thiểu một chút cho chắc: " + Describe(recommended));
                if (r.Alone.Concat([r.Cheapest, r.Recommended]).Any(p => p is not null && p.Changes.Any(c => c.Factor == "gpa" && c.To >= 4m)))
                    lines.Add("Lưu ý: GPA 4,0 là mức tối đa của thang điểm, nên cách nâng GPA ở trên gần như không thực hiện được nếu GPA của bạn đã cao.");
                break;
            case "blocked":
                var strengthen = StrengthenWithinGroup(r);
                if (strengthen.Count > 0)
                {
                    lines.Add("Nhóm sẽ không đổi, nhưng bạn mạnh hơn nếu:");
                    lines.AddRange(strengthen.Select(s => $"- {s}"));
                }
                else if (r.Comparison is { Count: > 0 })
                {
                    lines.Add("Hồ sơ của bạn đã ngang hoặc cao hơn mặt bằng của trường ở các mục trên, nên không còn mục nào cần nâng thêm.");
                }
                break;
            case "no_data":
                lines.Add("Trường chưa có đủ số liệu nên mình chưa tính được mức cần đạt.");
                break;
        }

        if (EnglishLine(r) is { } english) lines.Add(english);
        // Chi phí chỉ nói chi tiết khi là rào cản; luôn nêu ngân sách của học sinh trước khi nói vượt
        if (r.Budget.Status is "need_aid" or "over_budget") lines.Add("Về chi phí: " + Finance(r.Budget, r.Basis == "simulated"));
        return lines;
    }

    // Trường chưa nằm trong danh sách vì tiếng Anh: nói rõ cần đạt mức nào (một bài thi là đủ). Cùng nội dung với bảng
    private static string? EnglishLine(SchoolGoalResult r)
    {
        if (r.FilterReason is not ("ENGLISH" or "ENGLISH_NO_CERT")) return null;
        var parts = new List<string>();
        if (r.English?.MinIelts is { } ielts) parts.Add($"IELTS {ielts.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"))}");
        if (r.English?.MinToefl is { } toefl) parts.Add($"TOEFL {toefl:0}");
        if (r.English?.MinDuolingo is { } duolingo) parts.Add($"Duolingo {duolingo:0}");
        var need = parts.Count switch
        {
            0 => "",
            1 => $" Cần đạt {parts[0]}.",
            _ => $" Cần đạt {string.Join(", ", parts.Take(parts.Count - 1))} hoặc {parts[^1]} (một bài là đủ).",
        };
        var head = r.FilterReason == "ENGLISH_NO_CERT"
            ? "Tiếng Anh: bạn chưa có chứng chỉ tiếng Anh nên trường chưa nằm trong danh sách."
            : "Tiếng Anh: điểm của bạn chưa đủ nên trường chưa nằm trong danh sách.";
        return head + need + " Phần học thuật ở trên tính khi bạn đã đủ tiếng Anh.";
    }

    // Lời khuyên nộp theo vai trò của trường với học sinh (Khó vào là thử sức, Vừa sức là mục tiêu chính, An toàn hơn là chắc chắn)
    private static List<string> HowToApply(SchoolGoalResult r)
    {
        if (r.Current == "NA" || IsExcluded(r) || r.Outcome == "no_data") return [];
        var others = r.Alternatives.Count > 0 ? string.Join(", ", r.Alternatives.Select(a => a.Name)) : null;
        var advice = r.Current switch
        {
            "R" => $"Coi {r.Name} là trường thử sức, đừng dựa vào trường này; giữ thêm các trường Vừa sức và An toàn hơn trong danh sách."
                   + (others is null ? "" : $" Các trường đang ở nhóm {GroupName(r.Target)} với bạn: {others}."),
            "M" => $"Coi {r.Name} là một trường mục tiêu chính."
                   + (others is null
                       ? " Giữ thêm các trường An toàn hơn trong danh sách để chắc chắn."
                       : $" Giữ thêm các trường An toàn hơn như {others} để chắc chắn."),
            _ => $"{r.Name} là một trường khá chắc chắn trong danh sách của bạn.",
        };
        return ["Nên nộp thế nào:", advice];
    }

    // Toàn bộ lời giải thích theo thứ tự đã nêu ở Summarize
    private static List<string> Body(SchoolGoalResult r)
    {
        var lines = new List<string> { $"**{Headline(r)}**" };
        lines.AddRange(WhyNot(r));
        lines.AddRange(WhereYouAre(r));
        lines.AddRange(HowToImprove(r));
        lines.AddRange(HowToApply(r));
        return lines;
    }

    /// <summary>
    /// Dữ liệu cho Claude diễn đạt lại: tên trường đã hiểu, nhóm đích, các dữ kiện đã tính (chính là các dòng của lời dự phòng), các trường cùng
    /// nhóm đích, và tên các trường khác để chặn nhắc tên lạ. Dấu in đậm và gạch đầu dòng bị bỏ, vì Claude tự trình bày.
    /// </summary>
    public static GoalNoteRequestDto Note(SchoolGoalResult r, IEnumerable<string> allSchoolNames)
    {
        var alternatives = r.Alternatives.Select(a => a.Name).ToList();
        var used = new HashSet<string>(alternatives, StringComparer.OrdinalIgnoreCase) { r.Name };
        var others = allSchoolNames.Where(n => !used.Contains(n)).Distinct().Take(MaxOthers).ToList();
        var facts = Body(r)
            .Select(line => line.Replace("**", ""))
            .Select(line => line.StartsWith("- ") ? line[2..] : line)
            .ToList();
        return new GoalNoteRequestDto(r.Name, GroupName(r.Target), r.Outcome, facts, alternatives, others);
    }

    /// <summary>Công cụ mở khung kết quả trên trang gợi ý: mã trường và nhóm đích.</summary>
    // code và target: mở khung kết quả trên trang gợi ý. result: toàn bộ kết quả code đã tính (camelCase), để khung chat dựng thẻ
    // (vị trí của trường, các mức đích, tiếng Anh, tài chính, trường tương tự) mà không phải hỏi lại backend
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public const string EditProfileHref = "/analysis?edit=1";

    // basisLabel: mô tả giá trị giả định đã dùng ("ngân sách 25.000 USD mỗi năm") khi result.Basis là simulated, để thẻ ghi rõ
    public static Widget WidgetOf(SchoolGoalResult result, string? basisLabel = null)
    {
        var data = new JsonObject
        {
            ["code"] = result.Code,
            ["target"] = TargetCode(result.Target),
            ["result"] = JsonSerializer.SerializeToNode(result, WebJson),
            ["basisLabel"] = result.Basis == "simulated" ? basisLabel : null,
        };
        return new Widget(WidgetType, JsonSerializer.SerializeToElement(data));
    }

    /// <summary>
    /// Câu mở đầu nói rõ goal-seeking tính theo cơ sở nào, chỉ khi trang đang xem giả định (không thì không có gì để nhầm).
    /// simulated: đã tính theo giả định; real mà viewing có giá trị: tính theo hồ sơ thật dù trang đang giả định.
    /// </summary>
    public static string? BasisLine(string basis, string? viewingLabel) =>
        viewingLabel is null
            ? null
            : basis == "simulated"
                ? $"Mình tính theo giả định bạn đang xem ({viewingLabel}), không phải hồ sơ thật."
                : $"Mình tính theo hồ sơ thật của bạn, không phải giả định đang xem ({viewingLabel}).";

    // Mã nhóm của kết quả (R, M, S) → mã API (reach, match, safety)
    private static string TargetCode(string group) => group switch
    {
        "R" => "reach",
        "S" => "safety",
        _ => "match",
    };
}
