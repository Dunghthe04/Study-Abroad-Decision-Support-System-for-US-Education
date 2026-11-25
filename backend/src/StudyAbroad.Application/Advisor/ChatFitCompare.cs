using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// [UC-154] So sánh 2 đến 4 trường theo độ phù hợp với hồ sơ ("BU hay NYU hợp với em hơn"): nhóm và điểm năm thành phần của từng trường,
/// do code tính bằng công thức gợi ý (UC-22). Khác ChatSchoolInfo.Compare là so sánh số liệu của trường (học phí, hạn nộp...) không dùng hồ sơ (UC-156).
/// AI chỉ diễn giải số đã tính, không chọn trường và không đổi nhóm.
/// </summary>
public static class ChatFitCompare
{
    public const string Action = "school_fit_compare";
    public const string WidgetType = "fit_compare";
    public const string RecommendationsHref = "/recommendations";
    public const string OpenRecommendationsLabel = "Mở trang Gợi ý trường";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static string GroupLabel(FitCompareSchoolDto s) => s.Status switch
    {
        FitCompareStatus.Aid => "Cần học bổng lớn",
        FitCompareStatus.Excluded => "Không nằm trong gợi ý",
        _ => s.Group switch
        {
            "R" => "Khó vào",
            "M" => "Vừa sức",
            "S" => "An toàn hơn",
            _ => "Chưa đủ dữ liệu",
        },
    };

    // Lý do trường bị loại, nói bằng lời học sinh hiểu. Mã lạ giữ nguyên (không đoán nghĩa)
    private static string ExcludeText(string? reason) => reason switch
    {
        "MAJOR" => "trường không có ngành bạn chọn",
        "STATE" => "trường nằm ngoài các bang bạn chọn",
        "CONTROL" => "trường khác loại trường bạn chọn (công lập hoặc tư thục)",
        "ENGLISH" => "điểm tiếng Anh của bạn thấp hơn mức tối thiểu của trường từ 1 band trở lên",
        "ENGLISH_NO_CERT" => "bạn chưa có chứng chỉ tiếng Anh",
        "BUDGET" => "chi phí vượt ngân sách của bạn",
        null or "" => "không đạt điều kiện lọc",
        _ => reason,
    };

    private static readonly Dictionary<string, string> PartLabel = new()
    {
        ["position"] = "vừa tầm học thuật",
        ["budget"] = "ngân sách",
        ["quality"] = "chất lượng trường",
        ["aid"] = "học bổng",
        ["english"] = "tiếng Anh",
    };

    private static string Points(double? score) => score is double v ? $"{Math.Round(v * 100):0}" : "chưa có số liệu";

    // Mỗi trường một dòng, có điểm chung và năm thành phần. Dùng cho câu trả lời thay thế (khi AI lỗi) và làm số liệu gửi AI
    private static string Line(FitCompareSchoolDto s)
    {
        if (s.Status == FitCompareStatus.Excluded)
            return $"{s.Name}: không nằm trong gợi ý vì {ExcludeText(s.ExcludeReason)}.";
        var total = s.Total is double t ? $"điểm phù hợp {Math.Round(t * 100):0}/100" : "chưa tính được điểm phù hợp";
        var parts = string.Join(", ", s.Parts.Select(p => $"{PartLabel.GetValueOrDefault(p.Key, p.Key)} {Points(p.Score)}"));
        return $"{s.Name}: nhóm {GroupLabel(s)}, {total}" + (parts.Length > 0 ? $" ({parts})" : "") + ".";
    }

    /// <summary>
    /// Số liệu gửi AI để diễn giải, theo dạng "mục: giá trị từng trường" như so sánh số liệu (UC-156): nhóm, điểm phù hợp, thành phần thấp nhất của từng trường
    /// (code tìm sẵn để AI không tự so), trường bị loại kèm lý do, và nguyên tắc điểm chỉ sắp xếp trong cùng nhóm.
    /// </summary>
    public static IReadOnlyList<string> Facts(FitCompareResult result)
    {
        var ranked = result.Schools.Where(s => s.Status != FitCompareStatus.Excluded).ToList();
        var facts = new List<string>();
        if (ranked.Count > 0)
        {
            facts.Add($"Nhóm: {string.Join(" · ", ranked.Select(s => $"{s.Name} {GroupLabel(s)}"))}");
            facts.Add($"Điểm phù hợp: {string.Join(" · ", ranked.Select(s => $"{s.Name} {(s.Total is double t ? $"{Math.Round(t * 100):0}/100" : "chưa tính được")}"))}");
            var lowest = ranked.Select(s => (s.Name, Low: LowestPart(s))).Where(x => x.Low is not null).ToList();
            if (lowest.Count > 0)
                facts.Add($"Thành phần điểm thấp nhất: {string.Join(" · ", lowest.Select(x => $"{x.Name} {x.Low}"))}");
        }
        facts.AddRange(result.Schools.Where(s => s.Status == FitCompareStatus.Excluded).Select(Line));
        facts.Add("Điểm phù hợp chỉ dùng để sắp xếp các trường trong cùng một nhóm, không làm trường đổi nhóm.");
        return facts;
    }

    // Thành phần có điểm thấp nhất (nhiều thành phần bằng nhau thì nêu hết); thành phần thiếu số liệu không tính
    private static string? LowestPart(FitCompareSchoolDto s)
    {
        var scored = s.Parts.Where(p => p.Score is not null).ToList();
        if (scored.Count == 0)
            return null;
        var low = scored.Min(p => p.Score!.Value);
        var names = scored.Where(p => p.Score!.Value == low).Select(p => PartLabel.GetValueOrDefault(p.Key, p.Key));
        return $"{string.Join(" và ", names)} ({Math.Round(low * 100):0})";
    }

    /// <summary>Câu trả lời bằng chữ: lời AI nếu có, không thì các dòng số liệu code dựng sẵn.</summary>
    public static string Text(FitCompareResult result, string? aiNote)
    {
        var lines = result.Schools.Select(s => $"- {Line(s)}");
        var body = string.Join("\n", lines);
        var footer = "Điểm phù hợp chỉ dùng để sắp xếp các trường trong cùng một nhóm; trường khác nhóm thì xem nhóm trước. Đây là mức tham chiếu, không phải cam kết đỗ.";
        return aiNote is null ? $"{body}\n{footer}" : $"{aiNote}\n\n{body}\n{footer}";
    }

    /// <summary>Thẻ bảng so sánh: các trường kèm nhóm và điểm năm thành phần (thang 0 đến 1), nhận xét AI nếu có.</summary>
    public static Widget WidgetOf(FitCompareResult result, string? aiNote, string coveredText)
    {
        var data = new JsonObject
        {
            ["schools"] = JsonSerializer.SerializeToNode(result.Schools, WebJson),
            ["priorityAdjusted"] = result.PriorityAdjusted,
            ["coveredText"] = coveredText,
        };
        if (aiNote is not null)
            data["aiNote"] = aiNote;
        return new Widget(WidgetType, JsonSerializer.SerializeToElement(data));
    }

    /// <summary>Sau bảng: nút mở trang Gợi ý trường (xem đủ kết quả), rồi cần gì để vào từng trường trong hai trường đầu còn trong gợi ý.</summary>
    public static IReadOnlyList<QuickReply> NextSteps(FitCompareResult result) =>
    [
        new QuickReply(OpenRecommendationsLabel, OpenRecommendationsLabel, Href: RecommendationsHref),
        .. result.Schools.Where(s => s.Status != FitCompareStatus.Excluded).Take(2).Select(s => ChatNextSteps.GoalFor(s.Code, s.Name)),
    ];
}
