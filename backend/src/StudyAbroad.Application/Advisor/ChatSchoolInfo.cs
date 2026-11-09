using System.Globalization;
using System.Text;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.Application.Advisor;

/// <summary>Mục thông tin trường mà học sinh có thể hỏi và hệ thống có số liệu (SchoolInfoDto).</summary>
public enum InfoField
{
    Cost,
    Aid,
    English,
    Test,
    Gpa,
    Acceptance,
    Deadline,
    Outcomes,
    Majors,
    Ranking,
    Location,
    Website,
}

/// <summary>Câu trả lời về một trường. Complete = false khi hệ thống không có thông tin học sinh hỏi (để nhánh gọi thử kho kiến thức).</summary>
public record SchoolInfoAnswer(string Text, bool Complete);

/// <summary>
/// Trả lời "học phí Harvard bao nhiêu", "UCLA có học bổng không"... bằng số thật trong cơ sở dữ liệu, do code dựng, không qua AI nên không bịa.
/// Hiểu mục nào được hỏi bằng từ khóa. Không nhận ra mục nào thì tóm tắt ngắn kèm nút để hỏi tiếp.
/// Mục hệ thống không lưu (ký túc xá, việc làm, đời sống...) thì nói rõ là chưa có và chỉ website, không đoán.
/// </summary>
public static class ChatSchoolInfo
{
    public const string Action = "school_info";
    public const string SearchAction = "school_search";
    public const string SearchWidgetType = "school_search";
    public const int SearchTextItems = 5;
    public const string CompareAction = "school_compare";
    public const string CompareWidgetType = "school_compare";
    public const int MaxCompare = 4;
    public const string WidgetType = "school_info";
    public const int MaxMajors = 10;
    public const int MaxRankings = 4;
    private const string Verify = "Bạn nên kiểm tra lại trên trang chính thức của trường vì số liệu có thể đã thay đổi.";

    private static readonly (InfoField Field, string[] Words)[] Keywords =
    [
        (InfoField.Cost, ["hoc phi", "chi phi", "tong chi", "bao nhieu tien", "sinh hoat", "tien an o", "tuition", "cost", "phi hoc"]),
        (InfoField.Aid, ["hoc bong", "scholarship", "ho tro tai chinh", "financial aid"]),
        (InfoField.English, ["ielts", "toefl", "duolingo", "tieng anh", "yeu cau anh"]),
        (InfoField.Test, ["sat", "act", "diem thi", "test optional", "test-optional", "chinh sach thi", "khong can sat", "co can sat", "diem chuan hoa"]),
        (InfoField.Gpa, ["gpa", "diem trung binh", "diem gpa"]),
        (InfoField.Acceptance, ["ty le nhan", "ty le do", "ty le chon", "ty le chap nhan", "de vao", "kho vao", "acceptance", "ty le dau"]),
        (InfoField.Deadline, ["han nop", "deadline", "nop khi nao", "han chot", "early decision", "early action", "ngay nop", "nop truoc"]),
        (InfoField.Outcomes, ["ty le tot nghiep", "tot nghiep", "graduation", "giu lai", "retention"]),
        (InfoField.Majors, ["nganh", "chuyen nganh", "major"]),
        (InfoField.Ranking, ["xep hang", "ranking", "rank", "hang may", "us news", "u.s. news", "top may"]),
        (InfoField.Location, ["o dau", "bang nao", "thanh pho", "o bang", "vi tri"]),
        (InfoField.Website, ["website", "trang web", "link", "dia chi web"]),
    ];

    // Những điều học sinh hay hỏi nhưng bảng dữ liệu trường không có cột
    private static readonly string[] Unstored =
    [
        "ky tuc xa", "nha o", "dorm", "viec lam", "luong", "thoi tiet", "khi hau", "doi song", "cau lac bo", "clb",
        "the thao", "van hoa", "an ninh", "mon an", "co so vat chat", "giang vien", "si so",
    ];

    /// <summary>
    /// Dữ liệu advisor (hoặc nút) gửi sang: Question là câu hỏi, School là tên trường SLM trích (có thể trống), Code là mã trường (nút gửi thẳng),
    /// KbAnswer là câu trả lời của kho kiến thức trong cùng lượt (để thay hoặc giữ), KbFound = kho có nguồn thật.
    /// </summary>
    public record Query(
        string Question, string? School, string? Code, string? KbAnswer, bool KbFound,
        IReadOnlyList<string> SlmFields, bool KbSkipped);

    // Mã mục model trả (advisor: INFO_FIELDS) → mục số liệu. "khac" không có trong bảng: điều hệ thống không lưu
    private static readonly Dictionary<string, InfoField> SlmFieldCodes = new()
    {
        ["chi_phi"] = InfoField.Cost,
        ["hoc_bong"] = InfoField.Aid,
        ["tieng_anh"] = InfoField.English,
        ["diem_thi"] = InfoField.Test,
        ["gpa"] = InfoField.Gpa,
        ["ty_le_nhan"] = InfoField.Acceptance,
        ["han_nop"] = InfoField.Deadline,
        ["tot_nghiep"] = InfoField.Outcomes,
        ["nganh"] = InfoField.Majors,
        ["xep_hang"] = InfoField.Ranking,
        ["vi_tri"] = InfoField.Location,
        ["website"] = InfoField.Website,
    };

    /// <summary>Mục model trả, đổi sang mục số liệu, theo thứ tự cố định của danh sách mục. Mã lạ hoặc "khac" bị bỏ.</summary>
    public static IReadOnlyList<InfoField> FieldsFromModel(IEnumerable<string> codes)
    {
        var wanted = codes.Select(c => SlmFieldCodes.GetValueOrDefault(c, (InfoField)(-1))).Where(f => (int)f >= 0).ToHashSet();
        return Enum.GetValues<InfoField>().Where(wanted.Contains).ToList();
    }

    /// <summary>Model báo câu có điều hệ thống không lưu (mã "khac")?</summary>
    public static bool ModelSaysUnstored(IEnumerable<string> codes) => codes.Contains("khac");

    /// <summary>Dữ liệu so sánh: Question là câu hỏi, Schools là tên các trường như học sinh gõ, Fields là mã mục model trả, KbAnswer là câu tạm của advisor.</summary>
    public record CompareQuery(string Question, IReadOnlyList<string> Schools, IReadOnlyList<string> Fields, string? KbAnswer);

    public static CompareQuery? ParseCompare(System.Text.Json.JsonElement? data)
    {
        if (data is not { ValueKind: System.Text.Json.JsonValueKind.Object } d)
            return null;
        List<string> List(string name) =>
            d.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Array
                ? v.EnumerateArray().Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                    .Select(e => e.GetString()!.Trim()).ToList()
                : [];
        var schools = List("schools");
        if (schools.Count < 2)
            return null;
        string? Str(string name) =>
            d.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
        return new CompareQuery(Str("question") ?? "", schools, List("fields"), Str("kb_answer"));
    }

    // ---------------------------------------------------------------- tìm trường theo điều kiện

    /// <summary>Dữ liệu tìm trường: Question là câu hỏi, Filters là bộ lọc advisor đã chuẩn hóa, Sort là cách xếp, Unresolved là điều chưa đọc được, KbAnswer là câu tạm.</summary>
    public record SearchQuery(
        string Question, SchoolSearchFilters Filters, SchoolSort Sort, IReadOnlyList<string> Unresolved, string? KbAnswer);

    public static SearchQuery? ParseSearch(System.Text.Json.JsonElement? data)
    {
        if (data is not { ValueKind: System.Text.Json.JsonValueKind.Object } d)
            return null;
        string? Str(string name) =>
            d.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
        SchoolSearchFilters filters;
        try
        {
            filters = d.TryGetProperty("filters", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.Object
                ? System.Text.Json.JsonSerializer.Deserialize<SchoolSearchFilters>(f.GetRawText(), WebJson) ?? new SchoolSearchFilters()
                : new SchoolSearchFilters();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
        var unresolved = d.TryGetProperty("unresolved", out var u) && u.ValueKind == System.Text.Json.JsonValueKind.Array
            ? u.EnumerateArray().Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : [];
        var sort = Str("sort")?.ToLowerInvariant() switch
        {
            "lowestcost" => SchoolSort.LowestCost,
            "easiestadmission" => SchoolSort.EasiestAdmission,
            "mostselective" => SchoolSort.MostSelective,
            _ => SchoolSort.Quality,
        };
        return new SearchQuery(Str("question") ?? "", filters, sort, unresolved, Str("kb_answer"));
    }

    /// <summary>Có ít nhất một điều kiện lọc không (nếu không thì không có gì để tìm).</summary>
    public static bool HasAnyFilter(SchoolSearchFilters f) =>
        f.States is { Count: > 0 } || !string.IsNullOrWhiteSpace(f.City) || !string.IsNullOrWhiteSpace(f.Major)
        || !string.IsNullOrWhiteSpace(f.Control) || !string.IsNullOrWhiteSpace(f.Aid) || !string.IsNullOrWhiteSpace(f.TestPolicy)
        || f.MaxCostUsd is not null || f.MaxIelts is not null || f.MaxToefl is not null || f.MaxDuolingo is not null
        || f.MinAcceptRate is not null || f.MaxAcceptRate is not null || f.MaxRank is not null || f.MinGradRate is not null || f.Ivy;

    /// <summary>Nói lại các điều kiện đang lọc bằng lời, để học sinh kiểm tra hệ thống hiểu đúng.</summary>
    public static string FilterSummary(SchoolSearchFilters f)
    {
        var parts = new List<string>();
        if (f.States is { Count: > 0 }) parts.Add($"ở {string.Join(" hoặc ", f.States)}");
        if (!string.IsNullOrWhiteSpace(f.City)) parts.Add($"ở thành phố {f.City}");
        if (!string.IsNullOrWhiteSpace(f.Major)) parts.Add($"có ngành {f.Major}");
        if (f.Control == "public") parts.Add("công lập");
        if (f.Control == "private") parts.Add("tư thục");
        if (f.Aid == "any") parts.Add("có học bổng cho du học sinh");
        if (f.Aid == "need") parts.Add("có học bổng theo nhu cầu");
        if (f.Aid == "merit") parts.Add("có học bổng theo thành tích");
        if (f.TestPolicy == "optional") parts.Add("không bắt buộc SAT, ACT");
        if (f.TestPolicy == "required") parts.Add("bắt buộc SAT, ACT");
        if (f.MaxCostUsd is { } cost) parts.Add($"chi phí không quá {Money(cost)} USD mỗi năm");
        if (f.MaxIelts is { } ielts) parts.Add($"IELTS yêu cầu không quá {Score(ielts)}");
        if (f.MaxToefl is { } toefl) parts.Add($"TOEFL yêu cầu không quá {Score(toefl)}");
        if (f.MaxDuolingo is { } duo) parts.Add($"Duolingo yêu cầu không quá {Score(duo)}");
        if (f.MinAcceptRate is { } min) parts.Add($"tỷ lệ nhận từ {Pct(min)} trở lên");
        if (f.MaxAcceptRate is { } max) parts.Add($"tỷ lệ nhận không quá {Pct(max)}");
        if (f.MaxRank is { } rank) parts.Add($"top {rank} bảng xếp hạng");
        if (f.MinGradRate is { } grad) parts.Add($"tỷ lệ tốt nghiệp từ {Pct(grad)} trở lên");
        if (f.Ivy) parts.Add("thuộc Ivy League");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Thẻ danh sách tìm trường: toàn bộ kết quả (tối đa 100) để giao diện hiện 5 trường đầu và mở phần còn lại bằng "Xem thêm", không gửi thêm tin.
    /// coveredText là đoạn chữ trả lời mà thẻ đã nói đủ.
    /// </summary>
    public static Widget WidgetOfSearch(SchoolSearchResult result, SchoolSearchFilters filters, SchoolSort sort, string coveredText)
    {
        var data = new System.Text.Json.Nodes.JsonObject
        {
            ["total"] = result.Total,
            ["summary"] = FilterSummary(filters),
            ["sortLabel"] = SortLabel(sort),
            ["visible"] = SearchTextItems,
            ["items"] = System.Text.Json.JsonSerializer.SerializeToNode(result.Items, WebJson),
            ["coveredText"] = coveredText,
        };
        return new Widget(SearchWidgetType, System.Text.Json.JsonSerializer.SerializeToElement(data));
    }

    /// <summary>
    /// Nút bước tiếp theo sau danh sách: so sánh hai trường đầu (chạy theo mã, không qua model), rồi gợi ý trường theo hồ sơ riêng.
    /// Danh sách ngắn hơn hai trường thì không có nút so sánh.
    /// </summary>
    public static IReadOnlyList<QuickReply> SearchNextSteps(SchoolSearchResult result)
    {
        var steps = new List<QuickReply>();
        if (result.Items.Count >= 2)
        {
            var (a, b) = (result.Items[0], result.Items[1]);
            steps.Add(new QuickReply(
                "So sánh hai trường đầu", $"So sánh {a.Name} và {b.Name}",
                new ChatAction(CompareAction, SchoolCodes: [a.Code, b.Code], Question: $"So sánh {a.Name} và {b.Name}")));
        }
        steps.Add(new QuickReply("Gợi ý trường theo hồ sơ của em", "Gợi ý trường phù hợp với hồ sơ của em"));
        return steps;
    }

    private static string SortLabel(SchoolSort sort) => sort switch
    {
        SchoolSort.LowestCost => "chi phí thấp đến cao",
        SchoolSort.EasiestAdmission => "tỷ lệ nhận cao đến thấp (dễ vào trước)",
        SchoolSort.MostSelective => "tỷ lệ nhận thấp đến cao (chọn lọc trước)",
        _ => "xếp hạng (chất lượng) cao đến thấp",
    };

    // Tên điều kiện nói với học sinh khi gợi ý nới (khóa của hợp đồng với model)
    private static string FilterName(string key) => key switch
    {
        "bang" => "bang",
        "thanh_pho" => "thành phố",
        "nganh" => "ngành",
        "loai_truong" => "loại trường",
        "hoc_bong" => "học bổng",
        "chinh_sach_thi" => "chính sách SAT, ACT",
        "chi_phi_toi_da" => "mức chi phí",
        "ielts_toi_da" => "mức IELTS",
        "toefl_toi_da" => "mức TOEFL",
        "duolingo_toi_da" => "mức Duolingo",
        "ty_le_nhan_toi_thieu" or "ty_le_nhan_toi_da" => "tỷ lệ nhận",
        "xep_hang_toi_da" => "thứ hạng",
        "ty_le_tot_nghiep_toi_thieu" => "tỷ lệ tốt nghiệp",
        "ivy" => "Ivy League",
        _ => key,
    };

    public static string SearchUnresolved(IReadOnlyList<string> unresolved) =>
        $"Mình chưa hiểu chắc điều kiện {string.Join(", ", unresolved)} nên chưa tìm được. Bạn nói lại rõ hơn giúp mình nhé "
        + "(ví dụ tên bang bằng tiếng Anh, mức chi phí bằng USD).";

    public const string SearchNoFilter =
        "Bạn muốn lọc trường theo điều kiện nào? Ví dụ: bang, ngành, công lập hay tư thục, có học bổng, không cần SAT, chi phí tối đa.";

    /// <summary>
    /// Câu trả lời tìm trường: số trường tìm được, điều kiện đã hiểu, cách xếp và 5 trường đầu. Danh sách đầy đủ nằm ở thẻ (nút "Xem thêm").
    /// Không có trường nào thì nêu điều kiện nào nên bỏ để có kết quả.
    /// </summary>
    public static string SearchText(SchoolSearchResult result, SchoolSearchFilters filters, SchoolSort sort)
    {
        var summary = FilterSummary(filters);
        if (result.Total == 0)
        {
            var text = $"Không có trường nào thỏa tất cả điều kiện: {summary}.";
            if (result.Relaxed.Count > 0)
                text += " " + string.Join(" ", result.Relaxed.Take(3).Select(r => $"Nếu bỏ điều kiện {FilterName(r.Filter)} thì có {r.Total} trường."));
            return text;
        }
        var lines = new List<string> { $"Có {result.Total} trường {summary}. Xếp theo {SortLabel(sort)}." };
        var shown = result.Items.Take(SearchTextItems).ToList();
        for (var i = 0; i < shown.Count; i++)
            lines.Add($"{i + 1}. {SearchLine(shown[i])}");
        if (result.Total > shown.Count)
            lines.Add($"Và {result.Total - shown.Count} trường khác.");
        return string.Join("\n", lines);
    }

    private static string SearchLine(SchoolSearchItem s)
    {
        var place = string.Join(", ", new[] { s.City, s.State }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var control = ControlText(s.Control);
        var head = string.Join(", ", new[] { place, control }.Where(x => x.Length > 0));
        var facts = new List<string>();
        facts.Add(s.CostUsd is { } c ? $"chi phí {Money(c)} USD" : "chưa có chi phí");
        if (s.AcceptRate is { } r) facts.Add($"tỷ lệ nhận {Pct(r)}");
        if (s.BestRank is { } rank) facts.Add($"hạng #{rank}");
        return $"{s.Name}" + (head.Length > 0 ? $" ({head})" : "") + $": {string.Join(" · ", facts)}";
    }

    // Các mục so sánh khi học sinh không nói rõ mục nào
    public static readonly IReadOnlyList<InfoField> CompareDefaultFields =
        [InfoField.Cost, InfoField.Aid, InfoField.English, InfoField.Acceptance, InfoField.Ranking];

    public static string CompareUnresolved(IReadOnlyList<string> names) =>
        $"Mình chưa xác định được trường {string.Join(", ", names.Select(n => $"“{n}”"))} để so sánh. Bạn gõ tên đầy đủ của trường giúp mình nhé.";

    // Nhãn đứng trước phần nhận xét do AI viết, để học sinh phân biệt với bảng số liệu do hệ thống tra
    public const string CompareNoteLabel = "AI nhận xét:";

    public const string CompareNeedsTwo = "Để so sánh mình cần hai trường khác nhau, bạn nói rõ giúp mình nhé.";

    // Giá trị ngắn của một mục cho bảng so sánh: số liệu, không câu chữ dài. Thiếu số liệu thì "chưa có".
    private static string ShortValue(SchoolInfoDto s, InfoField field)
    {
        const string none = "chưa có số liệu";
        switch (field)
        {
            case InfoField.Cost:
                return s.Cost.Total is { } t ? $"{Money(t)} USD" : none;
            case InfoField.Aid:
                return s.Aid.Policy switch
                {
                    "none" => "không có học bổng cho du học sinh",
                    null => s.Aid.AvgUsd is { } a ? $"trung bình {Money(a)} USD" : none,
                    _ => s.Aid.AvgUsd is { } avg ? $"có, trung bình {Money(avg)} USD" : "có",
                };
            case InfoField.English:
            {
                var tests = new List<string>();
                if (s.English.MinIelts is { } ielts) tests.Add($"IELTS {Score(ielts)}");
                if (s.English.MinToefl is { } toefl) tests.Add($"TOEFL {Score(toefl)}");
                if (s.English.MinDuolingo is { } duo) tests.Add($"Duolingo {Score(duo)}");
                return tests.Count > 0 ? string.Join(" / ", tests) : none;
            }
            case InfoField.Test:
            {
                var policy = s.Test.SatPolicy switch { "required" => "bắt buộc", "optional" => "không bắt buộc", "not_accepted" => "không nhận", _ => null };
                var range = s.Test.Sat25 is { } a && s.Test.Sat75 is { } b ? $"SAT {a}–{b}" : null;
                return policy is null && range is null ? none : string.Join(", ", new[] { policy, range }.Where(x => x is not null));
            }
            case InfoField.Gpa:
                return s.Gpa?.Avg is { } g ? $"{g.ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"))} (thang 4)" : none;
            case InfoField.Acceptance:
                return s.Admission.AcceptRate is { } r
                    ? $"{Pct(r)}" + (s.Admission.IntlAcceptRate is { } i ? $" (du học sinh {Pct(i)})" : "")
                    : none;
            case InfoField.Deadline:
                return s.Deadlines.Count > 0 ? string.Join(", ", s.Deadlines.Select(d => $"{d.Round} {Date(d.Date)}")) : none;
            case InfoField.Outcomes:
                return s.Outcomes.GradRate6Yr is { } grad ? $"tốt nghiệp 6 năm {Pct(grad)}" : none;
            case InfoField.Majors:
                return s.Majors.Count > 0 ? $"{s.Majors.Count} ngành" : none;
            case InfoField.Ranking:
                return s.Rankings.Count > 0
                    ? $"{(string.IsNullOrWhiteSpace(s.Rankings[0].RankDisplay) ? $"#{s.Rankings[0].Rank}" : s.Rankings[0].RankDisplay)} {s.Rankings[0].ListName}"
                    : none;
            case InfoField.Location:
                return Where(s) is { Length: > 0 } where ? where.TrimEnd('.') : none;
            case InfoField.Website:
                return string.IsNullOrWhiteSpace(s.Website) ? none : s.Website;
            default:
                return none;
        }
    }

    private static string CompareLabel(InfoField field) => field switch
    {
        InfoField.Cost => "Chi phí mỗi năm",
        InfoField.Aid => "Học bổng cho du học sinh",
        InfoField.English => "Tiếng Anh tối thiểu",
        InfoField.Test => "Điểm thi",
        InfoField.Gpa => "GPA sinh viên trúng tuyển",
        InfoField.Acceptance => "Tỷ lệ nhận",
        InfoField.Deadline => "Hạn nộp",
        InfoField.Outcomes => "Tốt nghiệp",
        InfoField.Majors => "Ngành học",
        InfoField.Ranking => "Xếp hạng",
        InfoField.Location => "Vị trí",
        InfoField.Website => "Website",
        _ => field.ToString(),
    };

    /// <summary>
    /// So sánh số liệu thật của hai trường trở lên theo từng mục, do code dựng. Chi phí có thêm câu nói trường nào rẻ hơn bao nhiêu.
    /// Chỉ nêu chênh lệch khi cả hai có số liệu; không kết luận trường nào "tốt hơn" (đó là nhận xét, việc của phần AI).
    /// </summary>
    public static string Compare(IReadOnlyList<SchoolInfoDto> schools, IReadOnlyList<InfoField> fields)
    {
        var rows = CompareRows(schools, fields);
        var lines = new List<string> { $"So sánh {string.Join(" và ", schools.Select(s => s.Name))}:" };
        foreach (var row in rows)
        {
            lines.Add($"- {row.Label}: {string.Join(" · ", schools.Select((s, i) => $"{s.Name} {row.Values[i]}"))}");
            if (row.Gap is not null)
                lines.Add($"  {row.Gap}");
        }
        if (NeedsVerifyNote(rows))
            lines.Add(Verify);
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Câu trả lời so sánh khi AI viết được lời: chỉ lời của AI (đã nêu đủ số liệu của từng mục), kèm lời nhắc kiểm tra lại nếu có chi phí hoặc hạn nộp.
    /// Bảng do code dựng chỉ dùng làm câu thay thế khi AI lỗi, không hiện cùng lời AI để khỏi nói hai lần.
    /// </summary>
    public static string CompareAnswerFromNote(IReadOnlyList<SchoolInfoDto> schools, IReadOnlyList<InfoField> fields, string note) =>
        NeedsVerifyNote(CompareRows(schools, fields)) ? $"{note}\n\n{Verify}" : note;

    /// <summary>Một hàng của bảng so sánh: mục, nhãn, giá trị ngắn của từng trường (cùng thứ tự các trường), và câu chênh lệch chi phí nếu là hàng chi phí.</summary>
    public record CompareRow(string Field, string Label, IReadOnlyList<string> Values, string? Gap);

    // Chữ, thẻ và số liệu gửi Gemini đều dựng từ các hàng này nên không thể lệch nhau
    public static IReadOnlyList<CompareRow> CompareRows(IReadOnlyList<SchoolInfoDto> schools, IReadOnlyList<InfoField> fields) =>
        (fields.Count > 0 ? fields : CompareDefaultFields)
            .Select(f => new CompareRow(
                f.ToString().ToLowerInvariant(), CompareLabel(f), schools.Select(s => ShortValue(s, f)).ToList(),
                f == InfoField.Cost ? CostGap(schools) : null))
            .ToList();

    // Chi phí và hạn nộp có thể đổi nên kèm lời nhắc kiểm tra lại
    private static bool NeedsVerifyNote(IReadOnlyList<CompareRow> rows) =>
        rows.Any(r => r.Field is "cost" or "deadline");

    /// <summary>
    /// Thẻ so sánh trong chat: tên các trường, các hàng (đã định dạng sẵn), nhận xét của AI nếu có, lời nhắc kiểm tra lại.
    /// coveredText là toàn bộ đoạn chữ thẻ đã nói đủ (bảng và nhận xét), để giao diện bỏ đoạn đó và chỉ hiện thẻ.
    /// </summary>
    public static Widget WidgetOfCompare(
        IReadOnlyList<SchoolInfoDto> schools, IReadOnlyList<InfoField> fields, string? aiNote, string coveredText)
    {
        var rows = CompareRows(schools, fields);
        var data = new System.Text.Json.Nodes.JsonObject
        {
            ["schools"] = System.Text.Json.JsonSerializer.SerializeToNode(
                schools.Select(s => new { s.Code, s.Name, s.Website }), WebJson),
            ["rows"] = System.Text.Json.JsonSerializer.SerializeToNode(rows, WebJson),
            ["verify"] = NeedsVerifyNote(rows),
            ["coveredText"] = coveredText,
        };
        if (aiNote is not null)
            data["aiNote"] = aiNote;
        return new Widget(CompareWidgetType, System.Text.Json.JsonSerializer.SerializeToElement(data));
    }

    /// <summary>
    /// Số liệu gửi Gemini để viết nhận xét: đúng các dòng của bảng (mỗi mục một dòng, câu chênh lệch chi phí), không kèm lời nhắc.
    /// Gemini chỉ được dùng số trong các dòng này, nên chênh lệch chi phí phải có sẵn ở đây chứ không để Gemini tự trừ.
    /// </summary>
    public static IReadOnlyList<string> CompareFacts(IReadOnlyList<SchoolInfoDto> schools, IReadOnlyList<InfoField> fields)
    {
        var facts = new List<string>();
        foreach (var row in CompareRows(schools, fields))
        {
            facts.Add($"{row.Label}: {string.Join(" · ", schools.Select((s, i) => $"{s.Name} {row.Values[i]}"))}");
            if (row.Gap is not null)
                facts.Add(row.Gap);
        }
        return facts;
    }

    // "A rẻ hơn B khoảng X USD mỗi năm" cho hai trường; từ ba trường thì nêu trường rẻ nhất và đắt nhất. Cần ít nhất hai trường có số liệu
    private static string? CostGap(IReadOnlyList<SchoolInfoDto> schools)
    {
        var priced = schools.Where(s => s.Cost.Total is not null).OrderBy(s => s.Cost.Total).ToList();
        if (priced.Count < 2)
            return null;
        var gap = priced[^1].Cost.Total!.Value - priced[0].Cost.Total!.Value;
        return gap == 0
            ? "Chi phí hai trường bằng nhau."
            : $"{priced[0].Name} rẻ hơn {priced[^1].Name} khoảng {Money(gap)} USD mỗi năm.";
    }

    public const string Unavailable = "Mình chưa tra được số liệu của trường này lúc này, bạn thử lại sau ít phút nhé.";

    public static string SchoolNotFound(string typed) =>
        $"Mình chưa thấy trường “{typed}” trong danh sách của hệ thống. Bạn kiểm tra lại tên trường giúp mình nhé.";

    public static Query? Parse(System.Text.Json.JsonElement? data)
    {
        if (data is not { ValueKind: System.Text.Json.JsonValueKind.Object } d)
            return null;
        string? Str(string name) =>
            d.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
                ? v.GetString()!.Trim()
                : null;
        var question = Str("question") ?? "";
        var code = Str("code");
        // Hành động của nút có thể không kèm câu hỏi (tóm tắt trường); lượt của advisor thì phải có câu hỏi
        if (question.Length == 0 && code is null)
            return null;
        bool Flag(string name) => d.TryGetProperty(name, out var f) && f.ValueKind == System.Text.Json.JsonValueKind.True;
        var slmFields = d.TryGetProperty("fields", out var fs) && fs.ValueKind == System.Text.Json.JsonValueKind.Array
            ? fs.EnumerateArray().Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : [];
        return new Query(question, Str("school"), code, Str("kb_answer"), Flag("kb_found"), slmFields, Flag("kb_skipped"));
    }

    // Câu hỏi về nhóm, về cách để vào trường, hay về mức hợp với hồ sơ: giữ nguyên luồng giải thích hoặc goal-seeking, không phải hỏi số liệu.
    // "để vào" kiểm trên chữ có dấu vì "dễ vào" (tỷ lệ nhận) bỏ dấu giống hệt.
    private static readonly string[] KeepFlow = ["vi sao", "tai sao", "can gi", "lam sao", "phai lam gi", "hop voi", "nhom "];

    private static bool AsksAboutGroupOrHowToGetIn(string message) =>
        message.ToLowerInvariant().Contains("để vào") || KeepFlow.Any(w => Plain(message).Contains(w));

    /// <summary>
    /// SLM có thể xếp câu hỏi số liệu một trường vào "giải thích" (ví dụ "UCLA xếp hạng bao nhiêu") hoặc goal-seeking ("MIT cần IELTS bao nhiêu").
    /// Câu nhắc mục có số liệu mà không hỏi về nhóm hay cách để vào thì chuyển sang trả lời số liệu; trường lấy từ điều advisor đã hiểu (mã hoặc tên).
    /// Trả null khi không phải câu hỏi số liệu (giữ luồng cũ).
    /// </summary>
    public static AdvisorChatResponse? Reroute(AdvisorChatResponse response, string? message)
    {
        // Có chữ số là học sinh đang nói số của chính mình ("GPA em 3.5", "IELTS 7.0"), không phải hỏi số liệu trường
        if (string.IsNullOrWhiteSpace(message) || message.Any(char.IsDigit) || FieldsOf(message).Count == 0 || AsksAboutGroupOrHowToGetIn(message))
            return null;
        var data = new System.Text.Json.Nodes.JsonObject { ["question"] = message.Trim(), ["kb_answer"] = response.Answer };
        if (response.ActionData is { ValueKind: System.Text.Json.JsonValueKind.Object } d)
        {
            if (d.TryGetProperty("code", out var code) && code.ValueKind == System.Text.Json.JsonValueKind.String)
                data["code"] = code.GetString();
            if (d.TryGetProperty("school", out var school) && school.ValueKind == System.Text.Json.JsonValueKind.String)
                data["school"] = school.GetString();
        }
        return response with { Action = Action, ActionData = System.Text.Json.JsonSerializer.SerializeToElement(data) };
    }

    private static string Plain(string text)
    {
        var d = text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    private static bool HasWord(string plain, string word) =>
        word.Contains(' ') || word.Contains('-')
            ? plain.Contains(word)
            : System.Text.RegularExpressions.Regex.IsMatch(plain, $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b");

    /// <summary>Các mục có số liệu mà câu hỏi nhắc tới, theo thứ tự cố định (không phải thứ tự trong câu).</summary>
    public static IReadOnlyList<InfoField> FieldsOf(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return [];
        var plain = Plain(question);
        return Keywords.Where(k => k.Words.Any(w => HasWord(plain, w))).Select(k => k.Field).ToList();
    }

    /// <summary>Câu hỏi có nhắc điều hệ thống không lưu (ký túc xá, việc làm, đời sống...) không.</summary>
    public static bool AsksUnstored(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return false;
        var plain = Plain(question);
        return Unstored.Any(w => HasWord(plain, w));
    }

    // ---------------------------------------------------------------- dựng câu trả lời

    private static string Money(decimal v) => ((long)Math.Round(v)).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");

    private static string Pct(decimal fraction) =>
        (Math.Round(fraction * 100, 1)).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN")) + "%";

    private static string Score(decimal v) => v.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN"));

    private static string ControlText(string? control) => control switch
    {
        "public" => "công lập",
        "private" => "tư thục",
        _ => "",
    };

    private static string Where(SchoolInfoDto s)
    {
        var place = string.Join(", ", new[] { s.City, s.State }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var control = ControlText(s.Control);
        return (place, control) switch
        {
            ("", "") => "",
            ("", _) => $"Trường {control}.",
            (_, "") => $"Ở {place}.",
            _ => $"Ở {place}, trường {control}.",
        };
    }

    private static string Date(string raw) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : raw;

    private static string? FieldLine(SchoolInfoDto s, InfoField field)
    {
        switch (field)
        {
            case InfoField.Cost:
            {
                var c = s.Cost;
                if (c.Tuition is null)
                    return "Chi phí: hệ thống chưa có số liệu học phí của trường này.";
                var parts = new List<string> { $"học phí {Money(c.Tuition.Value)} USD" };
                if (c.Living is { } living) parts.Add($"sinh hoạt {Money(living)} USD");
                // Phí bằng 0 là số trong dữ liệu nhưng không rõ là "không có phí" hay "đã gồm trong học phí": không nêu dòng này, tổng chi phí không đổi
                if (c.Fees is > 0 and var fees) parts.Add($"phí {Money(fees)} USD");
                if (c.Other is { } other) parts.Add($"chi phí khác {Money(other)} USD");
                var total = c.Total is { } t ? $", tổng khoảng {Money(t)} USD" : "";
                return $"Chi phí mỗi năm: {string.Join(", ", parts)}{total}.";
            }
            case InfoField.Aid:
            {
                var a = s.Aid;
                var policy = a.Policy switch
                {
                    "none" => "Trường không có học bổng cho du học sinh.",
                    "need_only" => "Học bổng cho du học sinh xét theo nhu cầu tài chính.",
                    "merit_only" => "Học bổng cho du học sinh xét theo thành tích.",
                    "need_and_merit" => "Học bổng cho du học sinh xét theo cả nhu cầu tài chính và thành tích.",
                    _ => null,
                };
                var average = a.Policy != "none" && a.AvgUsd is { } avg
                    ? $"Người được học bổng nhận trung bình khoảng {Money(avg)} USD mỗi năm"
                      + (a.Recipients is { } n ? $" ({n} người)" : "")
                      + ". Đây là mức trung bình của người được nhận, không phải mức bạn sẽ nhận."
                    : null;
                return policy is null && average is null
                    ? "Học bổng: hệ thống chưa có số liệu học bổng cho du học sinh của trường này."
                    : string.Join(" ", new[] { policy, average }.Where(x => x is not null));
            }
            case InfoField.English:
            {
                var e = s.English;
                var tests = new List<string>();
                if (e.MinIelts is { } ielts) tests.Add($"IELTS {Score(ielts)}");
                if (e.MinToefl is { } toefl) tests.Add($"TOEFL {Score(toefl)}");
                if (e.MinDuolingo is { } duo) tests.Add($"Duolingo {Score(duo)}");
                if (tests.Count == 0)
                    return "Tiếng Anh: hệ thống chưa có mức tối thiểu của trường này.";
                var joined = tests.Count == 1 ? tests[0] : $"{string.Join(", ", tests.Take(tests.Count - 1))} hoặc {tests[^1]}";
                return $"Tiếng Anh tối thiểu: {joined}" + (tests.Count > 1 ? " (một bài là đủ)." : ".");
            }
            case InfoField.Test:
            {
                var t = s.Test;
                var policy = t.SatPolicy switch
                {
                    "required" => "Trường bắt buộc nộp điểm SAT hoặc ACT.",
                    "optional" => "Trường không bắt buộc nộp SAT hoặc ACT (test-optional).",
                    "not_accepted" => "Trường không nhận điểm SAT và ACT.",
                    _ => null,
                };
                var ranges = new List<string>();
                if (t.Sat25 is { } s25 && t.Sat75 is { } s75) ranges.Add($"SAT {s25}–{s75}");
                if (t.Act25 is { } a25 && t.Act75 is { } a75) ranges.Add($"ACT {a25}–{a75}");
                var range = ranges.Count > 0 ? $"Điểm của sinh viên trúng tuyển (từ mốc 25% đến 75%): {string.Join(", ", ranges)}." : null;
                return policy is null && range is null
                    ? "Điểm thi: hệ thống chưa có số liệu của trường này."
                    : string.Join(" ", new[] { policy, range }.Where(x => x is not null));
            }
            case InfoField.Gpa:
            {
                var g = s.Gpa;
                if (g?.Avg is not { } avg)
                    return "GPA: hệ thống chưa có số liệu GPA của sinh viên trúng tuyển trường này.";
                var parts = new List<string> { $"GPA trung bình của sinh viên trúng tuyển khoảng {avg.ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"))} (thang 4)" };
                // Chia thành các nhóm không chồng nhau, cộng lại 100%: đạt 4.0, từ 3,75 đến 3,99, còn lại dưới 3,75
                if (g.ShareAbove375 is { } high && g.ShareFour is { } four && high >= four && high <= 1)
                    return string.Join(", ", parts) + $". Phân bố: {Pct(four)} đạt 4.0, {Pct(high - four)} từ 3,75 đến 3,99, {Pct(1 - high)} dưới 3,75.";
                if (g.ShareFour is { } onlyFour)
                    parts.Add($"{Pct(onlyFour)} có GPA 4.0");
                return string.Join(", ", parts) + ".";
            }
            case InfoField.Acceptance:
            {
                var a = s.Admission;
                if (a.AcceptRate is null && a.IntlAcceptRate is null)
                    return "Tỷ lệ nhận: hệ thống chưa có số liệu của trường này.";
                var parts = new List<string>();
                if (a.AcceptRate is { } r) parts.Add($"Trường nhận khoảng {Pct(r)} hồ sơ");
                if (a.IntlAcceptRate is { } i) parts.Add($"{(parts.Count > 0 ? "riêng" : "Tỷ lệ nhận")} du học sinh khoảng {Pct(i)}");
                return string.Join(", ", parts) + ".";
            }
            case InfoField.Deadline:
                return s.Deadlines.Count == 0
                    ? "Hạn nộp: hệ thống chưa có số liệu của trường này."
                    : $"Hạn nộp hồ sơ: {string.Join(", ", s.Deadlines.Select(d => $"{d.Round} {Date(d.Date)}"))}.";
            case InfoField.Outcomes:
            {
                var o = s.Outcomes;
                if (o.RetentionRate1Yr is null && o.GradRate6Yr is null)
                    return "Tỷ lệ tốt nghiệp: hệ thống chưa có số liệu của trường này.";
                var parts = new List<string>();
                if (o.RetentionRate1Yr is { } keep) parts.Add($"{Pct(keep)} sinh viên năm nhất học tiếp năm hai");
                if (o.GradRate6Yr is { } grad) parts.Add($"{Pct(grad)} tốt nghiệp trong 6 năm");
                return $"Kết quả học tập: {string.Join(", ", parts)}.";
            }
            case InfoField.Majors:
            {
                if (s.Majors.Count == 0)
                    return "Ngành học: hệ thống chưa có danh sách ngành của trường này.";
                var shown = s.Majors.Take(MaxMajors).ToList();
                var more = s.Majors.Count - shown.Count;
                return $"Ngành học (một số): {string.Join(", ", shown)}" + (more > 0 ? $" và {more} ngành khác." : ".");
            }
            case InfoField.Ranking:
            {
                if (s.Rankings.Count == 0)
                    return "Xếp hạng: hệ thống chưa có xếp hạng của trường này.";
                var shown = s.Rankings.Take(MaxRankings).Select(r =>
                {
                    var rank = string.IsNullOrWhiteSpace(r.RankDisplay) ? $"#{r.Rank}" : r.RankDisplay;
                    return $"{rank}{(r.IsTie && !rank.Contains("tie", StringComparison.OrdinalIgnoreCase) ? " (đồng hạng)" : "")} trong {r.ListName}";
                }).ToList();
                var more = s.Rankings.Count - shown.Count;
                return $"Xếp hạng: {string.Join("; ", shown)}" + (more > 0 ? $"; và {more} bảng khác." : ".");
            }
            case InfoField.Location:
                return Where(s) is { Length: > 0 } where ? where : "Vị trí: hệ thống chưa có số liệu của trường này.";
            case InfoField.Website:
                return string.IsNullOrWhiteSpace(s.Website)
                    ? "Website: hệ thống chưa có địa chỉ của trường này."
                    : $"Website: {s.Website}";
            default:
                return null;
        }
    }

    private static string SourceLine(SchoolInfoDto s)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.SourceUrl)) parts.Add($"nguồn {s.SourceUrl}");
        if (s.RetrievedAt is { } d) parts.Add($"cập nhật ngày {d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");
        return parts.Count == 0 ? "" : $"Số liệu: {string.Join(", ", parts)}.";
    }

    private static string Pointer(SchoolInfoDto s) =>
        string.IsNullOrWhiteSpace(s.Website) ? "" : $" Bạn có thể xem thêm ở {s.Website}.";

    /// <summary>
    /// Câu trả lời cho một trường. fields là các mục câu hỏi nhắc tới (FieldsOf); unstoredAsked là câu có nhắc điều hệ thống không lưu.
    /// Có mục có số liệu: trả lời các mục đó (Complete). Chỉ hỏi điều không lưu: nói chưa có và chỉ website (Complete = false).
    /// Không nhận ra mục nào: tóm tắt ngắn (Complete).
    /// </summary>
    public static SchoolInfoAnswer Answer(SchoolInfoDto school, IReadOnlyList<InfoField> fields, bool unstoredAsked)
    {
        var source = SourceLine(school);
        if (fields.Count == 0 && unstoredAsked)
            return new SchoolInfoAnswer($"Hệ thống chưa có thông tin này của {school.Name}.{Pointer(school)}", false);

        var shown = fields.Count > 0 ? fields : SummaryFields;
        var lines = new List<string> { fields.Count > 0 ? $"{school.Name}:" : $"Tóm tắt về {school.Name}:" };
        lines.AddRange(shown.Select(f => FieldLine(school, f)).OfType<string>().Select(l => $"- {l}"));
        if (unstoredAsked)
            lines.Add($"Phần còn lại bạn hỏi (như ký túc xá, đời sống) hệ thống chưa lưu.{Pointer(school)}");
        if (fields.Contains(InfoField.Deadline) || fields.Contains(InfoField.Cost))
            lines.Add(Verify);
        if (source.Length > 0)
            lines.Add(source);
        return new SchoolInfoAnswer(string.Join("\n", lines), true);
    }

    // Các mục trong bản tóm tắt khi học sinh không nói rõ mục nào
    public static readonly IReadOnlyList<InfoField> SummaryFields =
        [InfoField.Location, InfoField.Cost, InfoField.Acceptance, InfoField.English, InfoField.Aid];

    private static readonly System.Text.Json.JsonSerializerOptions WebJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>
    /// Thẻ hiển thị trong chat: toàn bộ số liệu của trường và các mục cần hiện (mã chữ thường như "cost", "aid").
    /// Thẻ là số liệu thật do code dựng, hiện phía trên câu trả lời bằng chữ.
    /// </summary>
    // coveredText: đoạn chữ trả lời mà thẻ đã nói đủ, để giao diện bỏ đoạn đó và chỉ hiện thẻ cho gọn (các đoạn khác của lượt vẫn hiện).
    // Null khi chữ có phần thẻ không nói (ví dụ lời "ký túc xá hệ thống chưa lưu"): giao diện giữ nguyên chữ.
    public static Widget WidgetOf(SchoolInfoDto info, IReadOnlyList<InfoField> fields, string? coveredText = null)
    {
        var shown = fields.Count > 0 ? fields : SummaryFields;
        var data = new System.Text.Json.Nodes.JsonObject
        {
            ["info"] = System.Text.Json.JsonSerializer.SerializeToNode(info, WebJson),
            ["fields"] = new System.Text.Json.Nodes.JsonArray(
                shown.Select(f => (System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create(f.ToString().ToLowerInvariant())).ToArray()),
        };
        if (coveredText is not null)
            data["coveredText"] = coveredText;
        return new Widget(WidgetType, System.Text.Json.JsonSerializer.SerializeToElement(data));
    }

    /// <summary>Nút hỏi tiếp sau bản tóm tắt: mỗi nút gửi một câu hỏi đầy đủ về đúng trường đó.</summary>
    public static IReadOnlyList<QuickReply> FieldButtons(string schoolName) =>
    [
        new("Học phí", $"Học phí {schoolName} bao nhiêu?"),
        new("Học bổng", $"{schoolName} có học bổng cho du học sinh không?"),
        new("Tiếng Anh cần", $"{schoolName} cần IELTS bao nhiêu?"),
        new("Hạn nộp", $"Hạn nộp hồ sơ của {schoolName} là khi nào?"),
        new("GPA", $"GPA của sinh viên trúng tuyển {schoolName} là bao nhiêu?"),
        new("Xếp hạng", $"{schoolName} xếp hạng bao nhiêu?"),
    ];

    /// <summary>Câu mở khi đã biết trường nhưng không rõ học sinh muốn biết mục nào: tóm tắt rồi để học sinh chọn mục.</summary>
    public static string NoTopicLead(string schoolName) =>
        $"Mình chưa rõ bạn muốn biết mục nào của {schoolName}. Đây là bản tóm tắt, bạn chọn một mục bên dưới nhé.";
}
