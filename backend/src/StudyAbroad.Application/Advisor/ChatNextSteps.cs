using System.Globalization;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Nút bước tiếp theo dùng chung cho các câu trả lời tư vấn. Nút do code chọn, không để AI bịa.
/// Nút không nêu tên trường: bấm vào thì mở ô tìm trường để học sinh tự chọn (ChatGoalSeek.SchoolPicker).
///
/// Đây là NƠI DUY NHẤT của backend tạo nút bước tiếp theo. Mỗi tình huống một hàm:
///   AfterRecommend       sau danh sách gợi ý trường theo hồ sơ
///   AfterExplain         sau giải thích một trường
///   AfterSimulate        sau thử kịch bản what-if
///   AfterGoalSeek        sau "cần gì để vào trường này"
///   AfterSearch          sau tìm trường theo điều kiện
///   AfterSchoolOverview  sau bản tóm tắt một trường (học sinh chưa hỏi mục cụ thể)
/// Nút chọn đáp án khi tên trường mơ hồ (ChatGoalSeek.Choices) là ô chọn đáp án, không phải bước tiếp theo, nên không ở đây.
/// Nút của câu hỏi hồ sơ nằm ở advisor (buttons.py).
/// </summary>
public static class ChatNextSteps
{
    public const string ExplainPickLabel = "Giải thích một trường";
    public const string GoalPickLabel = "Cần gì để vào trường em muốn?";
    public const string AnotherExplainLabel = "Giải thích trường khác";
    public const string CenterLabel = "Gợi ý trung tâm tư vấn";
    // Câu gửi qua SLM như mọi câu hỏi khác (cùng chữ với nút trung tâm của advisor)
    public const string CenterText = "Em muốn gặp tư vấn viên";

    public static QuickReply Center { get; } = new(CenterLabel, CenterText);

    // Mở ô chọn trường, chọn xong thì giải thích trường đó
    public static QuickReply ExplainPicker(string label = ExplainPickLabel, IReadOnlyList<string>? suggested = null) =>
        new(label, label, new ChatAction(ChatExplanation.Action, Suggested: suggested));

    // Mở ô chọn trường, chọn xong thì tính cần gì để vào trường đó
    public static QuickReply GoalPicker(IReadOnlyList<string>? suggested = null) =>
        new(GoalPickLabel, GoalPickLabel, new ChatAction(ChatGoalSeek.Action, Suggested: suggested));

    /// <summary>Thêm nút gặp tư vấn viên vào cuối danh sách nút có sẵn (không thêm lần hai).</summary>
    public static IReadOnlyList<QuickReply> WithCenter(IReadOnlyList<QuickReply>? steps) =>
        steps is null ? [Center] : steps.Any(s => s.Label == CenterLabel) ? steps : [.. steps, Center];

    // ----------------------------------------------------------------------------------------------------------------
    // Sau danh sách gợi ý theo hồ sơ
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>Sau danh sách trường: chọn một trường để xem lý do, hoặc xem cần gì để vào trường em muốn.</summary>
    public static IReadOnlyList<QuickReply> AfterRecommend(IReadOnlyList<string>? suggested = null) =>
        [ExplainPicker(suggested: suggested), GoalPicker(suggested)];

    // ----------------------------------------------------------------------------------------------------------------
    // Sau giải thích một trường
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>Sau khi giải thích một trường: cần gì để vào trường này, xem trường khác, gặp tư vấn viên.</summary>
    // suggested: vài trường đầu danh sách gần nhất (trừ trường vừa giải thích) để hiện sẵn khi mở ô chọn
    public static IReadOnlyList<QuickReply> AfterExplain(string schoolCode, IReadOnlyList<string>? suggested = null) =>
    [
        new QuickReply("Cần gì để vào trường này?", "Cần gì để vào trường này?", new ChatAction(ChatGoalSeek.Action, schoolCode)),
        ExplainPicker(AnotherExplainLabel, suggested),
        Center,
    ];

    // ----------------------------------------------------------------------------------------------------------------
    // Sau thử kịch bản what-if
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>Nút sau what-if, kèm nút trung tâm. Không có bước nào thì không có nút nào (null).</summary>
    public static IReadOnlyList<QuickReply>? AfterSimulate(SimulationResult result, string? beforeItemsJson = null)
    {
        var steps = SimulationSteps(result, beforeItemsJson);
        return steps.Count == 0 ? null : WithCenter(steps);
    }

    /// <summary>
    /// Nút bước tiếp theo sau một kết quả what-if, do code chọn từ chính kết quả (tối đa 2):
    /// hiểu vì sao trường thay đổi nhiều nhất đổi nhóm (hoặc đổi vị trí), rồi xem cần gì để vào một trường Khó vào.
    /// Nút thứ hai KHÔNG chọn sẵn trường (học sinh chưa nói quan tâm trường nào): mở ô chọn trường để họ tự chọn.
    /// Trường bị loại khỏi danh sách không có để giải thích nên không được đưa ra.
    /// </summary>
    public static IReadOnlyList<QuickReply> SimulationSteps(SimulationResult result, string? beforeItemsJson = null)
    {
        var steps = new List<QuickReply>();

        // Nút không nêu tên trường: bấm vào mở ô chọn, trong ô chỉ có các trường vừa đổi nhóm (hoặc đổi vị trí) để học sinh chọn
        var changed = result.Changes
            .Where(c => c.To != "OUT" && ChatSimulation.Rank(c.To) != ChatSimulation.Rank(c.From))
            .OrderByDescending(c => Math.Abs(ChatSimulation.Rank(c.To) - ChatSimulation.Rank(c.From)))
            .Select(c => c.Code)
            .ToList();
        if (changed.Count > 0)
        {
            steps.Add(ExplainSome("Vì sao các trường đổi nhóm?", changed));
        }
        else if (beforeItemsJson is not null)
        {
            var moved = ChatSimulation.Moves(JsonNode.Parse(beforeItemsJson) as JsonObject, JsonNode.Parse(result.ItemsJson) as JsonObject)
                .Select(m => m.Code).ToList();
            if (moved.Count > 0)
                steps.Add(ExplainSome("Vì sao các trường đổi vị trí?", moved));
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

    // Giải thích một trong các trường đã đổi: không kèm mã trường, chỉ kèm danh sách mã để ô chọn trường thu hẹp lại
    private static QuickReply ExplainSome(string label, IReadOnlyList<string> codes) =>
        new(label, label, new ChatAction("explain", SchoolCodes: codes.Take(ChatActions.MaxPickerCodes).ToList()));

    // ----------------------------------------------------------------------------------------------------------------
    // Sau "cần gì để vào trường này"
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Nút sau kết quả cần gì để vào, kèm nút trung tâm. Trường bị loại thì có nút sửa theo lý do loại (GoalSeekFixes);
    /// trường đang trong danh sách thì có thể giải thích vì sao xếp nhóm này.
    /// </summary>
    public static IReadOnlyList<QuickReply> AfterGoalSeek(SchoolGoalResult result)
    {
        var steps = GoalSeekFixes(result);
        if (steps.Count == 0)
            steps = [new QuickReply("Giải thích trường này", "Giải thích trường này", new ChatAction(ChatExplanation.Action, result.Code))];
        return WithCenter(steps);
    }

    /// <summary>
    /// Nút bước tiếp theo khi trường bị bộ lọc cứng loại, theo đúng lý do (không bịa nút khi trường đang trong danh sách):
    /// khác ngành, bang, loại trường thì sửa hồ sơ; thiếu tiếng Anh thì thử mức cần đạt; vượt ngân sách thì thử mức ngân sách tối thiểu.
    /// </summary>
    public static IReadOnlyList<QuickReply> GoalSeekFixes(SchoolGoalResult r)
    {
        if (r.Position is not { Status: "excluded" } position)
            return [];
        switch (position.ExcludeReason)
        {
            case "MAJOR" or "STATE" or "CONTROL":
                return [new QuickReply("Sửa hồ sơ", "Sửa hồ sơ", Href: ChatGoalSeek.EditProfileHref)];
            case "ENGLISH" or "ENGLISH_NO_CERT":
                {
                    var (test, min) = r.English switch
                    {
                        { MinIelts: { } v } => ("IELTS", v),
                        { MinToefl: { } v } => ("TOEFL", v),
                        { MinDuolingo: { } v } => ("Duolingo", v),
                        _ => ("", 0m),
                    };
                    if (test == "")
                        return [];
                    var number = min.ToString("0.#", CultureInfo.InvariantCulture);
                    return [new QuickReply($"Thử {test} {number}", $"Nếu {test} của em là {number} thì sao?")];
                }
            case "BUDGET" when r.Budget.MinBudgetUsd is { } needed:
                {
                    var amount = (long)Math.Ceiling(needed);
                    var shown = amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
                    return [new QuickReply($"Thử ngân sách {shown} USD", $"Nếu ngân sách của em là {amount} USD mỗi năm thì sao?")];
                }
            default:
                return [];
        }
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Sau tìm trường theo điều kiện
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Nút bước tiếp theo sau danh sách: so sánh hai trường đầu (chạy theo mã, không qua model), rồi gợi ý trường theo hồ sơ riêng.
    /// Danh sách ngắn hơn hai trường thì không có nút so sánh.
    /// </summary>
    public static IReadOnlyList<QuickReply> AfterSearch(SchoolSearchResult result, SchoolSort sort = SchoolSort.Quality)
    {
        var steps = new List<QuickReply>();
        // Đang xếp theo tiền: cho học sinh đổi cách tính giữa "chỉ học phí" và "tổng chi phí cả ăn ở, sinh hoạt" (cùng chiều cao/thấp)
        if (sort is SchoolSort.LowestTuition or SchoolSort.HighestTuition)
        {
            var dir = sort == SchoolSort.HighestTuition ? "cao đến thấp" : "thấp đến cao";
            steps.Add(new QuickReply("Tính cả ăn ở, sinh hoạt", $"Xếp theo tổng chi phí cả ăn ở, sinh hoạt {dir}"));
        }
        else if (sort is SchoolSort.LowestCost or SchoolSort.HighestCost)
        {
            var dir = sort == SchoolSort.HighestCost ? "cao đến thấp" : "thấp đến cao";
            steps.Add(new QuickReply("Chỉ tính học phí", $"Xếp theo học phí {dir}"));
        }
        if (result.Items.Count >= 2)
        {
            var (a, b) = (result.Items[0], result.Items[1]);
            steps.Add(new QuickReply(
                "So sánh hai trường đầu", $"So sánh {a.Name} và {b.Name}",
                new ChatAction(ChatSchoolInfo.CompareAction, SchoolCodes: [a.Code, b.Code], Question: $"So sánh {a.Name} và {b.Name}")));
        }
        steps.Add(new QuickReply("Gợi ý trường theo hồ sơ của em", "Gợi ý trường phù hợp với hồ sơ của em"));
        return steps;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Sau bản tóm tắt một trường
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>Nút hỏi tiếp sau bản tóm tắt: mỗi nút gửi một câu hỏi đầy đủ về đúng trường đó.</summary>
    public static IReadOnlyList<QuickReply> AfterSchoolOverview(string schoolName) =>
    [
        new("Học phí", $"Học phí {schoolName} bao nhiêu?"),
        new("Học bổng", $"{schoolName} có học bổng cho du học sinh không?"),
        new("Tiếng Anh cần", $"{schoolName} cần IELTS bao nhiêu?"),
        new("Hạn nộp", $"Hạn nộp hồ sơ của {schoolName} là khi nào?"),
        new("GPA", $"GPA của sinh viên trúng tuyển {schoolName} là bao nhiêu?"),
        new("Xếp hạng", $"{schoolName} xếp hạng bao nhiêu?"),
    ];
}
