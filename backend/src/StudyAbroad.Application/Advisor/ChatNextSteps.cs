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
///   AfterSchoolInfo      sau khi trả lời một mục cụ thể của một trường ("học phí Purdue")
///   AfterCompare         sau bảng so sánh các trường
///   AfterExplainFailed   giải thích một trường không được
///   SearchHelp           tìm trường chưa chạy được (chưa có điều kiện, điều kiện không đọc ra, 0 trường)
///   NoSchoolData         không lấy được số liệu trường, không nhận ra trường, so sánh thiếu trường
///   EditProfile          hồ sơ chưa lưu được
/// Quy tắc: câu trả lời nào đi qua đây cũng có ít nhất một nút. Nút gửi câu (send) phải là câu SLM đã đọc đúng
/// (đã thử bằng model), nút chạy hành động (ChatAction) thì không qua SLM.
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

    public static QuickReply Recommend { get; } = new("Gợi ý trường theo hồ sơ của em", "Gợi ý trường phù hợp với hồ sơ của em");

    // Mở form sửa hồ sơ (link, không qua chat)
    public static QuickReply EditProfile { get; } = new("Sửa hồ sơ", "Sửa hồ sơ", Href: ChatGoalSeek.EditProfileHref);

    // Mở ô chọn trường rồi tính cần gì để vào trường đó, hoặc chạy thẳng khi đã biết trường
    public static QuickReply GoalFor(string schoolCode, string schoolName)
    {
        var label = $"Cần gì để vào {schoolName}?";
        return new(label, label, new ChatAction(ChatGoalSeek.Action, schoolCode));
    }

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

    /// <summary>Giải thích không được: chọn trường khác để giải thích, hoặc gặp tư vấn viên.</summary>
    public static IReadOnlyList<QuickReply> AfterExplainFailed(IReadOnlyList<string>? suggested = null) =>
        [ExplainPicker(AnotherExplainLabel, suggested), Center];

    // ----------------------------------------------------------------------------------------------------------------
    // Sau thử kịch bản what-if
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Nút sau what-if, kèm nút trung tâm. Kết quả không có gì để giải thích thì vẫn cho xem cần gì để vào một trường
    /// (mở ô chọn trường), không để học sinh hết đường đi.
    /// </summary>
    public static IReadOnlyList<QuickReply> AfterSimulate(SimulationResult result, string? beforeItemsJson = null)
    {
        var steps = SimulationSteps(result, beforeItemsJson);
        return WithCenter(steps.Count == 0 ? [GoalPicker()] : steps);
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
                return [EditProfile];
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
        steps.Add(Recommend);
        return steps;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Sau bản tóm tắt một trường
    // ----------------------------------------------------------------------------------------------------------------

    // Mục của một trường mà học sinh hay hỏi tiếp, theo thứ tự hiện trên nút: mỗi nút gửi một câu hỏi đầy đủ về đúng trường đó
    private static readonly (InfoField Field, string Label, Func<string, string> Ask)[] FollowUpFields =
    [
        (InfoField.Cost, "Học phí", name => $"Học phí {name} bao nhiêu?"),
        (InfoField.Aid, "Học bổng", name => $"{name} có học bổng cho du học sinh không?"),
        (InfoField.English, "Tiếng Anh cần", name => $"{name} cần IELTS bao nhiêu?"),
        (InfoField.Deadline, "Hạn nộp", name => $"Hạn nộp hồ sơ của {name} là khi nào?"),
        (InfoField.Gpa, "GPA", name => $"GPA của sinh viên trúng tuyển {name} là bao nhiêu?"),
        (InfoField.Ranking, "Xếp hạng", name => $"{name} xếp hạng bao nhiêu?"),
    ];

    /// <summary>Nút hỏi tiếp sau bản tóm tắt: mỗi nút gửi một câu hỏi đầy đủ về đúng trường đó.</summary>
    public static IReadOnlyList<QuickReply> AfterSchoolOverview(string schoolName) =>
        FollowUpFields.Select(f => new QuickReply(f.Label, f.Ask(schoolName))).ToList();

    /// <summary>
    /// Sau khi trả lời một mục của một trường ("học phí Purdue"): cần gì để vào trường này, rồi tối đa 3 mục chưa hỏi.
    /// </summary>
    public static IReadOnlyList<QuickReply> AfterSchoolInfo(string schoolCode, string schoolName, IReadOnlyList<InfoField> asked) =>
    [
        GoalFor(schoolCode, schoolName),
        .. FollowUpFields.Where(f => !asked.Contains(f.Field)).Take(3).Select(f => new QuickReply(f.Label, f.Ask(schoolName))),
    ];

    // ----------------------------------------------------------------------------------------------------------------
    // Sau so sánh trường
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>Sau bảng so sánh: cần gì để vào từng trường (hai trường đầu), rồi gặp tư vấn viên.</summary>
    public static IReadOnlyList<QuickReply> AfterCompare(IEnumerable<(string Code, string Name)> schools) =>
        [.. schools.Take(2).Select(s => GoalFor(s.Code, s.Name)), Center];

    // ----------------------------------------------------------------------------------------------------------------
    // Không chạy được việc tìm, tra trường
    // ----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Tìm trường chưa chạy được (chưa có điều kiện, điều kiện không đọc ra, không có trường nào): đưa vài câu tìm mẫu
    /// để học sinh bấm hoặc gõ theo. Các câu này đã thử bằng model, đọc đúng thành tìm trường với điều kiện tương ứng.
    /// </summary>
    public static IReadOnlyList<QuickReply> SearchHelp() =>
    [
        new("Trường có học bổng", "Tìm trường có học bổng cho du học sinh"),
        new("Trường không cần SAT", "Tìm trường không cần SAT"),
        new("Trường công lập", "Tìm trường công lập"),
        Recommend,
    ];

    /// <summary>Không lấy được số liệu, không nhận ra trường, hoặc so sánh thiếu trường: quay về gợi ý theo hồ sơ.</summary>
    public static IReadOnlyList<QuickReply> NoSchoolData() => [Recommend, Center];
}
