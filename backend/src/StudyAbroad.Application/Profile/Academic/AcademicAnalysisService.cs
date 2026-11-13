using System.Globalization;
using System.Text;
using System.Text.Json;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Profile.Academic;

/// <summary>
/// [USAS-365] Service xử lý thuật toán phân tích điểm học thuật:
/// - Quy đổi GPA thang 4.0 theo chuẩn WES hoặc AppSetting
/// - Tính GPA unweighted (không tính weighted, kế hoạch GPA weighted mục 5)
/// - Phân nhóm môn học (STEM, Xã hội, Ngoại ngữ)
/// - Phân tích xu hướng 3 năm qua các kỳ học
/// </summary>
public class AcademicAnalysisService : IAcademicAnalysisService
{
    private readonly IAcademicAnalysisRepository _repository;
    private readonly IGradeScaleProvider? _gradeScale;

    public AcademicAnalysisService(IAcademicAnalysisRepository repository, IGradeScaleProvider? gradeScale = null)
    {
        _repository = repository;
        _gradeScale = gradeScale;
    }

    /// <summary>
    /// [USAS-365] Lấy danh sách bảng điểm chi tiết kèm thông tin phân nhóm và GPA 4.0 tính toán.
    /// </summary>
    public async Task<IReadOnlyList<TranscriptScoreDto>> GetScoresAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _repository.GetProfileByUserIdAsync(userId, ct);
        if (profile == null)
            return Array.Empty<TranscriptScoreDto>();

        var scores = await _repository.GetScoresByProfileIdAsync(profile.Id, ct);
        var scaleConfig = await GetGradeScaleConfigAsync(ct);

        return scores
            .OrderBy(s => s.TermOrder)
            .ThenBy(s => s.Subject)
            .Select(s =>
            {
                var gpa4 = ConvertScoreToGpa4(s.Score, scaleConfig);
                var groupKey = ClassifySubjectGroup(s.Subject);
                var groupName = GetSubjectGroupName(groupKey);
                return new TranscriptScoreDto(
                    s.Id,
                    s.TermName,
                    s.TermOrder,
                    s.Subject,
                    s.Score,
                    s.Credits,
                    gpa4,
                    groupKey,
                    groupName
                );
            })
            .ToList();
    }

    /// <summary>
    /// [USAS-365] Lưu danh sách đầu điểm bảng điểm của học sinh (phòng chống tấn công IDOR).
    /// </summary>
    public async Task<IReadOnlyList<TranscriptScoreDto>> SaveScoresAsync(Guid userId, BatchTranscriptScoresRequest request, CancellationToken ct = default)
    {
        if (request == null || request.Scores == null)
            throw new ArgumentException("Dữ liệu bảng điểm không được để trống.");

        // [USAS-365] Phòng chống IDOR: Đảm bảo học sinh chỉ cập nhật bảng điểm của chính mình
        var profile = await _repository.EnsureProfileExistsAsync(userId, ct);

        // [USAS-365] Defensive Validation: Kiểm tra từng đầu điểm
        var entities = new List<TranscriptScore>();
        foreach (var item in request.Scores)
        {
            if (string.IsNullOrWhiteSpace(item.TermName))
                throw new ArgumentException("Tên học kỳ không được để trống (ví dụ: Lớp 10 HK1).");

            if (item.TermOrder < 1 || item.TermOrder > 20)
                throw new ArgumentException("Thứ tự học kỳ (TermOrder) phải nằm trong khoảng từ 1 đến 20.");

            if (string.IsNullOrWhiteSpace(item.Subject))
                throw new ArgumentException("Tên môn học không được để trống.");

            if (item.Score < 0m || item.Score > 10m)
                throw new ArgumentException($"Điểm môn '{item.Subject}' ({item.Score}) không hợp lệ. Điểm hệ 10 phải từ 0.0 đến 10.0.");

            if (item.Credits.HasValue && (item.Credits.Value <= 0m || item.Credits.Value > 30m))
                throw new ArgumentException($"Số tín chỉ/hệ số của môn '{item.Subject}' phải lớn hơn 0 và nhỏ hơn hoặc bằng 30.");

            entities.Add(new TranscriptScore
            {
                Id = item.Id ?? Guid.NewGuid(),
                StudentProfileId = profile.Id,
                TermName = item.TermName.Trim(),
                TermOrder = item.TermOrder,
                Subject = item.Subject.Trim(),
                Score = Math.Round(item.Score, 2),
                Credits = item.Credits.HasValue ? Math.Round(item.Credits.Value, 1) : null
            });
        }

        await _repository.SaveScoresAsync(profile.Id, entities, ct);
        return await GetScoresAsync(userId, ct);
    }

    /// <summary>
    /// [USAS-365] Xóa một đầu điểm trong bảng điểm.
    /// </summary>
    public async Task DeleteScoreAsync(Guid userId, Guid scoreId, CancellationToken ct = default)
    {
        var profile = await _repository.GetProfileByUserIdAsync(userId, ct);
        if (profile == null)
            throw new KeyNotFoundException("Không tìm thấy hồ sơ học sinh.");

        await _repository.DeleteScoresAsync(profile.Id, new[] { scoreId }, ct);
    }

    /// <summary>
    /// [USAS-365] Thực hiện thuật toán phân tích học thuật:
    /// 1. Quy đổi GPA 4.0 theo cấu hình
    /// 2. Tính GPA unweighted
    /// 3. Phân nhóm môn (STEM, Xã hội, Ngoại ngữ)
    /// 4. Đánh giá xu hướng 3 năm
    /// 5. Lưu vào analysis_results (kind = 'gpa') và cập nhật overall_gpa trong student_profiles
    /// </summary>
    public async Task<AcademicAnalysisResponseDto> AnalyzeAcademicProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _repository.EnsureProfileExistsAsync(userId, ct);
        var scores = await _repository.GetScoresByProfileIdAsync(profile.Id, ct);

        if (scores.Count == 0)
        {
            return new AcademicAnalysisResponseDto(
                AnalysisId: Guid.NewGuid(),
                StudentProfileId: profile.Id,
                UnweightedGpa: 0m,
                RawAverage: 0m,
                TotalSubjects: 0,
                TotalTerms: 0,
                Trend: AcademicConstants.TrendConsistent,
                TrendDescription: "Chưa có dữ liệu bảng điểm. Vui lòng nhập điểm các học kỳ để tiến hành phân tích.",
                ScaleSource: "Chưa có dữ liệu",
                SubjectGroups: new List<SubjectGroupScoreDto>(),
                TermAverages: new List<TermTrendDto>(),
                Disclaimer: AcademicConstants.DefaultDisclaimer,
                CreatedAt: DateTime.UtcNow
            );
        }

        var scaleConfig = await GetGradeScaleConfigAsync(ct);

        // 1. Gắn nhóm môn cho từng điểm
        var scoreDetails = scores.Select(s => new
        {
            Score = s,
            GroupKey = ClassifySubjectGroup(s.Subject)
        }).ToList();

        // 2. Tính điểm trung bình hệ 10 và Unweighted GPA 4.0: lấy TB hệ 10 rồi đổi một lần (kế hoạch GPA weighted, mục 4)
        var rawAverage = Math.Round(scores.Average(s => s.Score), 2);
        var unweightedGpa = AverageToGpa4(scores.Select(s => s.Score), scaleConfig);

        // Không tính GPA weighted: học bạ Việt Nam không có AP/Honors, trường Mỹ đọc điểm quốc tế dạng unweighted
        // (kế hoạch GPA weighted, mục 5)

        // 3. Thống kê theo nhóm môn học
        var subjectGroups = scoreDetails
            .GroupBy(s => s.GroupKey)
            .Select(g =>
            {
                var gKey = g.Key;
                var gName = GetSubjectGroupName(gKey);
                var avgRaw = Math.Round(g.Average(x => x.Score.Score), 2);
                var avgGpa = AverageToGpa4(g.Select(x => x.Score.Score), scaleConfig);
                var distinctSubjects = g.Select(x => x.Score.Subject).Distinct().OrderBy(x => x).ToList();
                return new SubjectGroupScoreDto(gKey, gName, avgRaw, avgGpa, g.Count(), distinctSubjects);
            })
            .OrderBy(g => GetGroupDisplayOrder(g.GroupKey))
            .ToList();

        // 4. Thống kê theo từng học kỳ và đánh giá xu hướng 3 năm
        var termAverages = scoreDetails
            .GroupBy(s => new { s.Score.TermOrder, s.Score.TermName })
            .OrderBy(g => g.Key.TermOrder)
            .Select(g => new TermTrendDto(
                TermOrder: g.Key.TermOrder,
                TermName: g.Key.TermName,
                RawAverage: Math.Round(g.Average(x => x.Score.Score), 2),
                Gpa4: AverageToGpa4(g.Select(x => x.Score.Score), scaleConfig),
                SubjectCount: g.Count()
            ))
            .ToList();

        var (trend, trendDesc) = EvaluateTrend(termAverages);

        // 5. Đóng gói kết quả & lưu trữ vào analysis_results
        var analysisId = Guid.NewGuid();
        var analysisResult = new AnalysisResult
        {
            Id = analysisId,
            StudentProfileId = profile.Id,
            Kind = AcademicConstants.KindGpa,
            ModelVersion = AcademicConstants.DefaultModelVersion,
            ResultJson = JsonSerializer.Serialize(new
            {
                analysisId,
                studentProfileId = profile.Id,
                unweightedGpa,
                rawAverage,
                totalSubjects = scores.Count,
                totalTerms = termAverages.Count,
                trend,
                trendDescription = trendDesc,
                scaleSource = scaleConfig.SourceName,
                subjectGroups,
                termAverages,
                disclaimer = AcademicConstants.DefaultDisclaimer,
                createdAt = DateTime.UtcNow
            })
        };

        await _repository.SaveAnalysisResultAsync(analysisResult, unweightedGpa, ct);

        return new AcademicAnalysisResponseDto(
            AnalysisId: analysisId,
            StudentProfileId: profile.Id,
            UnweightedGpa: unweightedGpa,
            RawAverage: rawAverage,
            TotalSubjects: scores.Count,
            TotalTerms: termAverages.Count,
            Trend: trend,
            TrendDescription: trendDesc,
            ScaleSource: scaleConfig.SourceName,
            SubjectGroups: subjectGroups,
            TermAverages: termAverages,
            Disclaimer: AcademicConstants.DefaultDisclaimer,
            CreatedAt: DateTime.UtcNow
        );
    }

    /// <summary>
    /// [USAS-365] Lấy kết quả phân tích học thuật mới nhất đã lưu trong DB.
    /// </summary>
    public async Task<AcademicAnalysisResponseDto?> GetLatestAnalysisAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _repository.GetProfileByUserIdAsync(userId, ct);
        if (profile == null)
            return null;

        var latest = await _repository.GetLatestAnalysisAsync(profile.Id, ct);
        if (latest == null || string.IsNullOrWhiteSpace(latest.ResultJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(latest.ResultJson);
            var root = doc.RootElement;

            return new AcademicAnalysisResponseDto(
                AnalysisId: latest.Id,
                StudentProfileId: profile.Id,
                UnweightedGpa: root.GetProperty("unweightedGpa").GetDecimal(),
                RawAverage: root.GetProperty("rawAverage").GetDecimal(),
                TotalSubjects: root.GetProperty("totalSubjects").GetInt32(),
                TotalTerms: root.GetProperty("totalTerms").GetInt32(),
                Trend: root.GetProperty("trend").GetString() ?? AcademicConstants.TrendConsistent,
                TrendDescription: root.GetProperty("trendDescription").GetString() ?? "",
                ScaleSource: root.GetProperty("scaleSource").GetString() ?? "WES Standard Scale",
                SubjectGroups: JsonSerializer.Deserialize<List<SubjectGroupScoreDto>>(root.GetProperty("subjectGroups").GetRawText()) ?? new(),
                TermAverages: JsonSerializer.Deserialize<List<TermTrendDto>>(root.GetProperty("termAverages").GetRawText()) ?? new(),
                Disclaimer: root.TryGetProperty("disclaimer", out var dProp) ? dProp.GetString() ?? AcademicConstants.DefaultDisclaimer : AcademicConstants.DefaultDisclaimer,
                CreatedAt: latest.CreatedAt
            );
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// [USAS-365] Lấy cấu hình thang quy đổi điểm đọc từ app_settings (key: grade_scale.10).
    /// Nếu chưa cấu hình thì nạp bảng quy đổi WES chuẩn quốc tế.
    /// </summary>
    public async Task<GradeScaleConfigDto> GetGradeScaleConfigAsync(CancellationToken ct = default)
    {
        if (_gradeScale is not null)
            return await _gradeScale.GetAsync(ct);

        // Không có provider (test cũ): đọc thẳng DB, bảng sai luật thì về bảng mặc định
        var setting = await _repository.GetAppSettingByKeyAsync(AcademicConstants.GradeScale10AppSettingKey, ct);
        return GradeScale.Parse(setting?.ValueJson) ?? GetDefaultScaleConfig();
    }

    /// <summary>
    /// [USAS-365] Quy đổi một điểm hệ 10 sang thang 4.0 bằng nội suy giữa các mốc của bảng (mặc định mốc THPT EducationUSA), giống
    /// TranscriptGpa.To4, toGpa4 bên FE và gpa10_to_4 bên advisor. Không còn tra bậc thang, không còn ×0.4
    /// </summary>
    public static decimal ConvertScoreToGpa4(decimal rawScore, GradeScaleConfigDto config)
    {
        if (rawScore < 0m || rawScore > 10m)
            throw new ArgumentOutOfRangeException(nameof(rawScore), "Điểm số phải nằm trong khoảng từ 0.0 đến 10.0.");

        return GradeScale.Interpolate(rawScore, config);
    }

    /// <summary>
    /// GPA thang 4 của một nhóm điểm hệ 10 (cả bảng điểm, một nhóm môn, một học kỳ): lấy TB hệ 10 chưa làm tròn rồi đổi
    /// một lần, không đổi từng môn rồi mới lấy TB (kế hoạch GPA weighted, mục 4). Ví dụ 7.0 và 10 → TB 8.5 → 3.63, không phải 3.50
    /// </summary>
    public static decimal AverageToGpa4(IEnumerable<decimal> scores10, GradeScaleConfigDto config) =>
        Math.Round(ConvertScoreToGpa4(scores10.Average(), config), 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// [USAS-365] Bảng quy đổi mặc định: bảng bậc THPT của EducationUSA Vietnam 2025 (Đại sứ quán Hoa Kỳ), lấy cận dưới mỗi
    /// khoảng làm mốc: 5 → 2.0 (C), 6 → 3.0 (B), 8 → 3.5 (A), 9.5 → 4.0 (A+). Thêm 0 → 0 làm sàn (bảng gốc cho F = 1.0,
    /// hệ thống theo cách Mỹ để F = 0). Giữa hai mốc thì nội suy tuyến tính cho thoải, không nhảy bậc: 7.0 → 3.25.
    /// MaxScore = mốc kế tiếp − 0.01, chỉ để hiển thị
    /// </summary>
    public static GradeScaleConfigDto GetDefaultScaleConfig()
    {
        (decimal Min, decimal Gpa4, string Letter)[] anchors =
        [
            (0m, 0m, "F"), (5.0m, 2.0m, "C"), (6.0m, 3.0m, "B"), (8.0m, 3.5m, "A"), (9.5m, 4.0m, "A+"), (10m, 4.0m, "A+"),
        ];

        return new GradeScaleConfigDto(
            ScaleKey: AcademicConstants.GradeScale10AppSettingKey,
            SourceName: "EducationUSA Vietnam 2025 (bậc THPT), nội suy tuyến tính",
            Method: GradeScale.LinearMethod,
            Rules: anchors
                .Select((a, i) => new GradeScaleRuleDto(a.Min, i + 1 < anchors.Length ? anchors[i + 1].Min - 0.01m : 10m, a.Gpa4, a.Letter))
                .ToList()
        );
    }

    /// <summary>
    /// [USAS-365] Phân loại môn học thành các nhóm: Tự nhiên (STEM), Xã hội, Ngoại ngữ, Khác.
    /// </summary>
    public static string ClassifySubjectGroup(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return AcademicConstants.GroupOthers;

        var normalized = RemoveDiacritics(subject.Trim().ToLowerInvariant());

        // 1. Nhóm Ngoại ngữ
        if (normalized.Contains("anh") || normalized.Contains("english") ||
            normalized.Contains("phap") || normalized.Contains("french") ||
            normalized.Contains("trung") || normalized.Contains("chinese") ||
            normalized.Contains("nhat") || normalized.Contains("japanese") ||
            normalized.Contains("han") || normalized.Contains("korean") ||
            normalized.Contains("ngoai ngu"))
        {
            return AcademicConstants.GroupLanguages;
        }

        // 2. Nhóm Xã hội (kiểm tra trước để tránh 'dia ly' dính chữ 'ly')
        if (normalized.Contains("dia") || normalized.Contains("geography") ||
            normalized.Contains("su") || normalized.Contains("history") ||
            normalized.Contains("van") || normalized.Contains("literature") ||
            normalized.Contains("gdcd") || normalized.Contains("cong dan") || normalized.Contains("civics") ||
            normalized.Contains("triet") || normalized.Contains("kinh te") || normalized.Contains("economics"))
        {
            return AcademicConstants.GroupSocialSciences;
        }

        // 3. Nhóm Tự nhiên (STEM)
        bool isPhysics = normalized == "ly" || normalized.StartsWith("ly ") || normalized.EndsWith(" ly") ||
                         normalized.Contains("vat ly") || normalized.Contains("physics");

        if (normalized.Contains("toan") || normalized.Contains("math") || normalized.Contains("calculus") ||
            isPhysics ||
            normalized.Contains("hoa") || normalized.Contains("chemistry") ||
            normalized.Contains("sinh") || normalized.Contains("biology") ||
            normalized.Contains("tin") || normalized.Contains("cong nghe") ||
            normalized.Contains("computer") || normalized.Contains("it") || normalized.Contains("stem"))
        {
            return AcademicConstants.GroupNaturalSciences;
        }

        return AcademicConstants.GroupOthers;
    }

    public static string GetSubjectGroupName(string groupKey) => groupKey switch
    {
        AcademicConstants.GroupNaturalSciences => "Khoa học Tự nhiên (STEM)",
        AcademicConstants.GroupSocialSciences => "Khoa học Xã hội & Nhân văn",
        AcademicConstants.GroupLanguages => "Ngoại ngữ",
        _ => "Môn học khác"
    };

    private static int GetGroupDisplayOrder(string groupKey) => groupKey switch
    {
        AcademicConstants.GroupNaturalSciences => 1,
        AcademicConstants.GroupLanguages => 2,
        AcademicConstants.GroupSocialSciences => 3,
        _ => 4
    };

    /// <summary>
    /// [USAS-365] Thuật toán đánh giá xu hướng học tập dựa trên biến động điểm các kỳ.
    /// </summary>
    public static (string Trend, string Description) EvaluateTrend(IReadOnlyList<TermTrendDto> terms)
    {
        if (terms.Count < 2)
        {
            return (
                AcademicConstants.TrendConsistent,
                "Chưa đủ dữ liệu học kỳ để đánh giá xu hướng biến động (cần tối thiểu 2 học kỳ)."
            );
        }

        // Chia chuỗi các kỳ thành 2 nửa (kỳ đầu và kỳ cuối) để so sánh đà tiến bộ
        var half = terms.Count / 2;
        var firstHalfAvg = terms.Take(half).Average(t => t.Gpa4);
        var secondHalfAvg = terms.Skip(half).Average(t => t.Gpa4);
        var delta = secondHalfAvg - firstHalfAvg;

        if (delta >= 0.15m)
        {
            return (
                AcademicConstants.TrendUpward,
                $"Xu hướng tiến bộ tích cực (+{delta:F2} điểm GPA). Điểm số các kỳ sau cải thiện rõ rệt, đây là điểm cộng lớn trong hồ sơ du học Mỹ (thể hiện nỗ lực phát triển cá nhân)."
            );
        }

        if (delta <= -0.15m)
        {
            return (
                AcademicConstants.TrendDownward,
                $"Xu hướng điểm số giảm nhẹ ({delta:F2} điểm GPA) ở các kỳ gần đây. Bạn nên chuẩn bị giải trình hợp lý trong bài luận cá nhân (ví dụ: tập trung nghiên cứu, thi chứng chỉ hoặc tham gia ngoại khóa chuyên sâu)."
            );
        }

        return (
            AcademicConstants.TrendConsistent,
            "Điểm số duy trì phong độ rất ổn định qua các học kỳ, thể hiện năng lực học tập vững vàng và nhất quán."
        );
    }

    /// <summary>
    /// [USAS-365] Loại bỏ dấu tiếng Việt để chuẩn hóa việc nhận diện tên môn học.
    /// </summary>
    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

        for (int i = 0; i < normalizedString.Length; i++)
        {
            char c = normalizedString[i];
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("đ", "d")
            .Replace("Đ", "d");
    }
}
