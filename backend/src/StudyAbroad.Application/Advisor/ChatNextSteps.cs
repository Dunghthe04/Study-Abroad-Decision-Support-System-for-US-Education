using System.Globalization;
using System.Text.Json;
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
///   AfterSearch          sau tìm trường theo điều kiện (kèm nút lọc lại: top 20, top 50, mức chi phí)
///   AfterNoResult        tìm trường ra 0 trường (nút bỏ từng điều kiện, kèm số trường sẽ có)
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
    public static IReadOnlyList<QuickReply> AfterSearch(
        SchoolSearchResult result, SchoolSort sort = SchoolSort.Quality, SchoolSearchFilters? filters = null,
        IReadOnlyList<ChatSchoolInfo.IgnoredFilter>? ignored = null)
    {
        var steps = new List<QuickReply>();
        if (filters is not null)
            steps.AddRange(Refinements(result, sort, filters, ignored).Take(MaxRefinements));
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
        return steps.Take(MaxSteps).ToList();
    }

    public const int MaxSteps = 5;
    private const int MaxRefinements = 2;
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // Nút lọc lại: chưa giới hạn hạng mà danh sách dài (hoặc advisor vừa bỏ điều kiện hạng không đọc ra) thì gợi ý top 20, 50;
    // advisor vừa bỏ mức chi phí không đọc ra thì gợi ý vài mức chi phí
    private static IEnumerable<QuickReply> Refinements(
        SchoolSearchResult result, SchoolSort sort, SchoolSearchFilters f, IReadOnlyList<ChatSchoolInfo.IgnoredFilter>? ignored)
    {
        var dropped = (ignored ?? []).Select(i => i.Key).ToHashSet();
        if (f.MaxRank is null && (sort == SchoolSort.Quality || dropped.Contains("max_rank")))
        {
            foreach (var top in new[] { 20, 50 }.Where(top => result.Total > top))
                yield return SearchAgain($"Chỉ top {top}", $"Các trường trong top {top}", f with { MaxRank = top }, sort);
        }
        if (f.MaxCostUsd is null && dropped.Contains("max_cost"))
        {
            foreach (var cost in new[] { 30_000m, 40_000m })
            {
                var shown = cost.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
                yield return SearchAgain($"Dưới {shown} USD", $"Các trường có chi phí dưới {shown} USD mỗi năm", f with { MaxCostUsd = cost }, sort);
            }
        }
    }

    /// <summary>
    /// Tìm ra 0 trường: mỗi điều kiện bỏ đi mà có trường thì thành một nút (kèm số trường sẽ có), nhiều trường nhất trước.
    /// Không bỏ được điều kiện nào ra kết quả thì đưa câu tìm mẫu.
    /// </summary>
    public static IReadOnlyList<QuickReply> AfterNoResult(SchoolSearchResult result, SchoolSort sort, SchoolSearchFilters filters)
    {
        var relax = result.Relaxed
            .Where(r => r.Total > 0)
            .OrderByDescending(r => r.Total)
            .Take(MaxRefinements)
            .Select(r =>
            {
                var name = ChatSchoolInfo.FilterName(r.Filter);
                return SearchAgain($"Bỏ điều kiện {name} (có {r.Total} trường)", $"Bỏ điều kiện {name}", Without(filters, r.Filter), sort);
            })
            .ToList();
        return relax.Count == 0 ? SearchHelp() : [.. relax, Recommend];
    }

    // Nút tìm lại với bộ điều kiện mới: chạy thẳng không qua SLM. Chữ gửi đi là câu đầy đủ (hiện trong lịch sử chat, và nếu
    // dữ liệu nút không hợp lệ thì câu này đi qua advisor như học sinh tự gõ)
    public static QuickReply SearchAgain(string label, string sentence, SchoolSearchFilters filters, SchoolSort sort)
    {
        var sortCode = char.ToLowerInvariant(sort.ToString()[0]) + sort.ToString()[1..];
        var data = new JsonObject
        {
            ["question"] = sentence,
            ["filters"] = JsonSerializer.SerializeToNode(filters, WebJson),
            ["sort"] = sortCode,
            // giữ cách xếp đã chọn, và cho tìm được cả khi bỏ hết điều kiện lọc
            ["model_sort"] = sortCode,
        };
        return new QuickReply(label, sentence, new ChatAction(ChatSchoolInfo.SearchAction, Question: sentence, Search: JsonSerializer.SerializeToElement(data)));
    }

    // Bỏ một điều kiện theo mã điều kiện của bộ tìm (RecommendationService.SearchTests)
    public static SchoolSearchFilters Without(SchoolSearchFilters f, string key) => key switch
    {
        "bang" => f with { States = null },
        "thanh_pho" => f with { City = null },
        "nganh" => f with { Major = null },
        "loai_truong" => f with { Control = null },
        "hoc_bong" => f with { Aid = null },
        "chinh_sach_thi" => f with { TestPolicy = null },
        "chi_phi_toi_da" => f with { MaxCostUsd = null },
        "ielts_toi_da" => f with { MaxIelts = null },
        "toefl_toi_da" => f with { MaxToefl = null },
        "duolingo_toi_da" => f with { MaxDuolingo = null },
        "ty_le_nhan_toi_thieu" => f with { MinAcceptRate = null },
        "ty_le_nhan_toi_da" => f with { MaxAcceptRate = null },
        "xep_hang_toi_da" => f with { MaxRank = null },
        "ty_le_tot_nghiep_toi_thieu" => f with { MinGradRate = null },
        "ivy" => f with { Ivy = false },
        _ => f,
    };

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
