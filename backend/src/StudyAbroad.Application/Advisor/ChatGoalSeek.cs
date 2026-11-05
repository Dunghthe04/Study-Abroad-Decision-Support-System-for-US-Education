using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

/// <summary>Điều học sinh hỏi trong chat: tên trường (chữ thô SLM đã trích) và nhóm đích (reach | match | safety | null).</summary>
public record ChatGoalQuery(string School, string? Target);

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
    public const int MaxOthers = 400;

    /// <summary>Đọc ActionData của advisor. Null nếu thiếu tên trường.</summary>
    public static ChatGoalQuery? Parse(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } d
            || !d.TryGetProperty("school", out var s) || s.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(s.GetString()))
            return null;
        var target = d.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        return new ChatGoalQuery(s.GetString()!.Trim(), target);
    }

    // ---------------------------------------------------------------- không chắc là trường nào

    /// <summary>Không thấy trường nào khớp: nói rõ để học sinh gõ lại hoặc chọn từ danh sách.</summary>
    public static string NotFound(string text) =>
        $"Mình chưa thấy trường “{text}” trong danh sách của hệ thống. Bạn tìm trong ô bên dưới nhé.";

    /// <summary>Ô chọn trường có gợi ý (gõ đến đâu hiện đến đó). Data.target là nhóm đích học sinh đã nói, để câu hỏi gửi lại giữ nguyên.</summary>
    public static Widget SchoolPicker(string? target) =>
        new(PickerType, JsonSerializer.SerializeToElement(new JsonObject { ["target"] = target }));

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
        value is null ? "chưa có" : factor == "gpa" ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : ((int)Math.Round(value.Value)).ToString(CultureInfo.InvariantCulture);

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
    private static string Reason(string code) => code switch
    {
        GroupReasons.LowAcceptRate => "trường chỉ nhận dưới 15% hồ sơ nên luôn xếp Khó vào, điểm cao đến đâu cũng không đổi nhóm",
        GroupReasons.CappedSelective => "trường nhận dưới 50% hồ sơ nên chỉ tối đa là Vừa sức",
        GroupReasons.CappedWeakMetric => "một chỉ số của bạn còn thấp hơn mốc 25% của trường nên chưa tính là An toàn",
        GroupReasons.IntlDowngrade => "trường nhận du học sinh ít hơn hẳn tỷ lệ chung nên bị hạ một nhóm",
        GroupReasons.NeedTest => "trường bắt buộc nộp điểm SAT hoặc ACT mà hồ sơ của bạn chưa có",
        GroupReasons.NoAcademicData => "trường chưa có đủ số liệu học thuật để so sánh",
        _ => "",
    };

    private static string Describe(GoalPlanDto plan) =>
        string.Join(" và ", plan.Changes.Select(c => $"{FactorName(c.Factor)} {Number(c.Factor, c.From)} → {Number(c.Factor, c.To)}"))
        + $" ({Difficulty(plan.Difficulty)})";

    // Tài chính đứng trước học thuật
    private static string Finance(GoalBudgetDto b)
    {
        var cost = b.CostUsd is { } c ? $"{Money(c)} USD mỗi năm" : null;
        return b.Status switch
        {
            "ok" => $"Chi phí khoảng {cost}, nằm trong ngân sách của bạn.",
            "need_aid" => $"Chi phí khoảng {cost}, vượt ngân sách khoảng {Money(b.ShortfallUsd ?? 0)} USD: cần ngân sách từ {Money(b.MinBudgetUsd ?? 0)} USD mỗi năm hoặc học bổng lớn.",
            "over_budget" => $"Chi phí khoảng {cost}, vượt ngân sách và trường không có học bổng phù hợp nên không nằm trong danh sách của bạn. Cần ngân sách từ {Money(b.MinBudgetUsd ?? 0)} USD mỗi năm.",
            "no_budget" => $"Chi phí khoảng {cost}. Hồ sơ của bạn không đặt trần ngân sách mỗi năm.",
            _ => "Trường chưa có số liệu chi phí.",
        };
    }

    /// <summary>
    /// Câu trả lời ghép bằng code từ kết quả goal-seeking. Nói lại tên trường đã hiểu để học sinh sửa nếu nhầm.
    /// </summary>
    public static string Summarize(SchoolGoalResult r) =>
        string.Join("\n", [$"Mình hiểu bạn hỏi về {r.Name}, nhóm muốn đạt: {GroupName(r.Target)}.", .. Body(r), SeeTable]);

    private const string SeeTable = "Bạn xem bảng chi tiết và áp dụng thử vào thanh kéo ở trang Gợi ý trường nhé.";

    // Các dòng dữ kiện do code tính: tài chính, học thuật, trường tương tự. Vừa là thân câu dự phòng, vừa là dữ kiện gửi Gemini
    private static List<string> Body(SchoolGoalResult r)
    {
        var lines = new List<string> { "Tài chính: " + Finance(r.Budget) };

        switch (r.Outcome)
        {
            case "already_reached":
                lines.Add($"Học thuật: bạn đã ở nhóm {GroupName(r.Current)}, không cần thay đổi gì để đạt {GroupName(r.Target)}.");
                break;
            case "reachable":
                lines.Add($"Học thuật: để lên nhóm {GroupName(r.Target)}, mức tối thiểu khi chỉ đổi một yếu tố:");
                lines.AddRange(r.Alone.Select(p => "- " + Describe(p)));
                if (r.Alone.Count == 0)
                    lines.Add("- Một yếu tố riêng chưa đủ, cần kết hợp nhiều yếu tố.");
                if (r.Cheapest is { Changes.Count: > 1 } combined)
                    lines.Add("Kết hợp ít công sức nhất: " + Describe(combined));
                if (r.Recommended is { } recommended)
                    lines.Add("Nên đặt mục tiêu (có đệm): " + Describe(recommended));
                break;
            case "blocked":
                lines.Add("Học thuật: " + string.Join("; ", r.Reasons.Select(Reason).Where(s => s != "")) + ".");
                lines.Add($"Nhóm cao nhất bạn có thể đạt ở trường này là {GroupName(r.Best)}.");
                break;
            default:
                var why = r.Reasons.Select(Reason).FirstOrDefault(s => s != "") ?? "trường chưa đủ dữ liệu";
                lines.Add($"Học thuật: {why}, nên mình chưa tính được mức cần đạt.");
                break;
        }

        if (r.Alternatives.Count > 0)
            lines.Add("Trường tương tự đang ở nhóm này với bạn: " + string.Join(", ", r.Alternatives.Select(a => a.Name)) + ".");
        return lines;
    }

    /// <summary>
    /// Dữ liệu cho Gemini diễn đạt lại: tên trường đã hiểu, nhóm đích, các dữ kiện đã tính, trường tương tự, và tên các trường khác để chặn nhắc tên lạ.
    /// </summary>
    public static GoalNoteRequestDto Note(SchoolGoalResult r, IEnumerable<string> allSchoolNames)
    {
        var alternatives = r.Alternatives.Select(a => a.Name).ToList();
        var used = new HashSet<string>(alternatives, StringComparer.OrdinalIgnoreCase) { r.Name };
        var others = allSchoolNames.Where(n => !used.Contains(n)).Distinct().Take(MaxOthers).ToList();
        var facts = Body(r).Where(line => !line.StartsWith("Trường tương tự")).Select(line => line.StartsWith("- ") ? line[2..] : line).ToList();
        return new GoalNoteRequestDto(r.Name, GroupName(r.Target), r.Outcome, facts, alternatives, others);
    }

    /// <summary>Công cụ mở khung kết quả trên trang gợi ý: mã trường và nhóm đích.</summary>
    public static Widget WidgetOf(SchoolGoalResult result)
    {
        var data = new JsonObject { ["code"] = result.Code, ["target"] = TargetCode(result.Target) };
        return new Widget(WidgetType, JsonSerializer.SerializeToElement(data));
    }

    // Mã nhóm của kết quả (R, M, S) → mã API (reach, match, safety)
    private static string TargetCode(string group) => group switch
    {
        "R" => "reach",
        "S" => "safety",
        _ => "match",
    };
}
