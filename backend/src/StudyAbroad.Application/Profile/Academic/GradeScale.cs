using System.Text.Json;

namespace StudyAbroad.Application.Profile.Academic;

/// <summary>
/// Kiểm tra, đọc và quy đổi điểm thang 10 sang thang 4 theo bảng mốc (app_settings key grade_scale.10).
/// Một chỗ duy nhất cho luật nội suy, để AcademicAnalysisService, TranscriptGpa, chat và giả lập cùng ra một số.
/// </summary>
public static class GradeScale
{
    /// <summary>Method của bảng mốc nội suy. Bảng cũ lưu "table" là bảng tra bậc thang (chỉ còn dùng khi tính lại hồ sơ cũ).</summary>
    public const string LinearMethod = "linear";
    public const string LegacyTableMethod = "table";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Trả danh sách lỗi của bảng; rỗng là hợp lệ. Luật: có ít nhất một khoảng, mốc thấp nhất là 0,
    /// MinScore tăng dần thật sự trong 0–10, Gpa4 trong 0–4 và không giảm khi điểm tăng.
    /// </summary>
    public static IReadOnlyList<string> Validate(GradeScaleConfigDto? config)
    {
        var errors = new List<string>();
        if (config?.Rules is null || config.Rules.Count == 0)
        {
            errors.Add("Bảng quy đổi phải có ít nhất một khoảng điểm.");
            return errors;
        }

        if (config.Rules.Any(r => r is null))
        {
            errors.Add("Bảng quy đổi có khoảng điểm rỗng.");
            return errors;
        }

        foreach (var rule in config.Rules)
        {
            if (rule.MinScore < 0m || rule.MinScore > 10m)
                errors.Add($"Mốc điểm {rule.MinScore} phải nằm trong 0–10.");
            if (rule.Gpa4 < 0m || rule.Gpa4 > 4m)
                errors.Add($"GPA {rule.Gpa4} của mốc {rule.MinScore} phải nằm trong 0–4.");
        }

        var ordered = config.Rules.OrderBy(r => r.MinScore).ToList();
        if (ordered[0].MinScore != 0m)
            errors.Add("Mốc thấp nhất phải là 0 để điểm nào cũng có khoảng.");

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].MinScore == ordered[i - 1].MinScore)
                errors.Add($"Mốc điểm {ordered[i].MinScore} bị trùng.");
            else if (ordered[i].Gpa4 < ordered[i - 1].Gpa4)
                errors.Add($"GPA của mốc {ordered[i].MinScore} không được thấp hơn mốc {ordered[i - 1].MinScore}.");
        }

        return errors;
    }

    /// <summary>Đọc JSON trong app_settings; trả null nếu rỗng, sai cú pháp hoặc không hợp lệ.</summary>
    public static GradeScaleConfigDto? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var parsed = JsonSerializer.Deserialize<GradeScaleConfigDto>(json, ReadOptions);
            return Validate(parsed).Count == 0 ? parsed : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tra bậc thang kiểu cũ (bảng WES, Method "table"): lấy khoảng có MinScore lớn nhất mà điểm vẫn ≥ MinScore.
    /// Chỉ còn dùng để nhận ra hồ sơ đã tính bằng bảng cũ khi mod lưu bảng mới; quy đổi mới luôn dùng Interpolate.
    /// </summary>
    public static decimal Lookup(decimal score, GradeScaleConfigDto config) =>
        config.Rules
            .OrderByDescending(rule => rule.MinScore)
            .FirstOrDefault(rule => score >= rule.MinScore)?.Gpa4 ?? 0m;

    /// <summary>
    /// Nội suy tuyến tính giữa các mốc (MinScore → Gpa4), cách UC Irvine dùng cho điểm Việt Nam (kế hoạch GPA weighted,
    /// mục 4). Dưới mốc đầu lấy GPA mốc đầu, trên mốc cuối lấy GPA mốc cuối. Không làm tròn: chỉ làm tròn khi hiển thị.
    /// </summary>
    public static decimal Interpolate(decimal score, GradeScaleConfigDto config)
    {
        var anchors = config.Rules.OrderBy(rule => rule.MinScore).ToList();

        if (score <= anchors[0].MinScore)
            return anchors[0].Gpa4;
        if (score >= anchors[^1].MinScore)
            return anchors[^1].Gpa4;

        // Hai mốc kề nhau bao quanh điểm: low.MinScore ≤ score < high.MinScore
        var high = anchors.First(rule => rule.MinScore > score);
        var low = anchors[anchors.IndexOf(high) - 1];

        // Nhân trước rồi chia để số decimal giữ đúng (8.5 → 3.625)
        return low.Gpa4 + (score - low.MinScore) * (high.Gpa4 - low.Gpa4) / (high.MinScore - low.MinScore);
    }

    /// <summary>JSON của bảng mặc định, dùng để seed grade_scale.10 khi DB chưa có.</summary>
    public static string DefaultJson() =>
        JsonSerializer.Serialize(AcademicAnalysisService.GetDefaultScaleConfig(), WriteOptions);
}
