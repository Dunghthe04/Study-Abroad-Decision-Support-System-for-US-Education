using System.Globalization;
using System.Text;
using System.Text.Json;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Profile.Academic;

/// <summary>
/// [USAS-365] Service xử lý thuật toán phân tích điểm học thuật:
/// - Quy đổi GPA thang 4.0 theo chuẩn WES hoặc AppSetting
/// - Tính Unweighted GPA & Weighted GPA (theo tín chỉ / môn chuyên)
/// - Phân nhóm môn học (STEM, Xã hội, Ngoại ngữ)
/// - Phân tích xu hướng 3 năm qua các kỳ học
/// </summary>
public class AcademicAnalysisService : IAcademicAnalysisService
{
    private readonly IAcademicAnalysisRepository _repository;

    public AcademicAnalysisService(IAcademicAnalysisRepository repository)
    {
        _repository = repository;
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
    /// 2. Tính Unweighted & Weighted GPA
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
                WeightedGpa: 0m,
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

        // 1. Quy đổi điểm từng môn sang thang 4.0
        var scoreDetails = scores.Select(s => new
        {
            Score = s,
            Gpa4 = ConvertScoreToGpa4(s.Score, scaleConfig),
            GroupKey = ClassifySubjectGroup(s.Subject)
        }).ToList();

        // 2. Tính điểm trung bình hệ 10 và Unweighted GPA 4.0
        var rawAverage = Math.Round(scores.Average(s => s.Score), 2);
        var unweightedGpa = Math.Round(scoreDetails.Average(s => s.Gpa4), 2);

        // 3. Tính Weighted GPA
        // Nếu có credits thì tính theo tín chỉ, nếu không có credits thì thưởng 0.5 điểm cho môn chuyên/nâng cao
        decimal weightedGpa;
        var hasCredits = scores.Any(s => s.Credits.HasValue && s.Credits.Value > 0);
        if (hasCredits)
        {
            decimal totalWeightedPoints = 0m;
            decimal totalCredits = 0m;
            foreach (var item in scoreDetails)
            {
                var credit = item.Score.Credits.GetValueOrDefault(1m);
                totalWeightedPoints += item.Gpa4 * credit;
                totalCredits += credit;
            }
            weightedGpa = totalCredits > 0 ? Math.Round(totalWeightedPoints / totalCredits, 2) : unweightedGpa;
        }
        else
        {
            // Kiểm tra môn nâng cao/chuyên/AP/Honors (+0.5 điểm quy đổi, tối đa 4.5)
            var weightedScores = scoreDetails.Select(s =>
            {
                var isAdvanced = IsAdvancedSubject(s.Score.Subject);
                return isAdvanced ? Math.Min(4.5m, s.Gpa4 + 0.5m) : s.Gpa4;
            });
            weightedGpa = Math.Round(weightedScores.Average(), 2);
        }

        // 4. Thống kê theo nhóm môn học
        var subjectGroups = scoreDetails
            .GroupBy(s => s.GroupKey)
            .Select(g =>
            {
                var gKey = g.Key;
                var gName = GetSubjectGroupName(gKey);
                var avgRaw = Math.Round(g.Average(x => x.Score.Score), 2);
                var avgGpa = Math.Round(g.Average(x => x.Gpa4), 2);
                var distinctSubjects = g.Select(x => x.Score.Subject).Distinct().OrderBy(x => x).ToList();
                return new SubjectGroupScoreDto(gKey, gName, avgRaw, avgGpa, g.Count(), distinctSubjects);
            })
            .OrderBy(g => GetGroupDisplayOrder(g.GroupKey))
            .ToList();

        // 5. Thống kê theo từng học kỳ và đánh giá xu hướng 3 năm
        var termAverages = scoreDetails
            .GroupBy(s => new { s.Score.TermOrder, s.Score.TermName })
            .OrderBy(g => g.Key.TermOrder)
            .Select(g => new TermTrendDto(
                TermOrder: g.Key.TermOrder,
                TermName: g.Key.TermName,
                RawAverage: Math.Round(g.Average(x => x.Score.Score), 2),
                Gpa4: Math.Round(g.Average(x => x.Gpa4), 2),
                SubjectCount: g.Count()
            ))
            .ToList();

        var (trend, trendDesc) = EvaluateTrend(termAverages);

        // 6. Đóng gói kết quả & lưu trữ vào analysis_results
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
                weightedGpa,
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
            WeightedGpa: weightedGpa,
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
                WeightedGpa: root.GetProperty("weightedGpa").GetDecimal(),
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
        var setting = await _repository.GetAppSettingByKeyAsync(AcademicConstants.GradeScale10AppSettingKey, ct);
        if (setting != null && !string.IsNullOrWhiteSpace(setting.ValueJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<GradeScaleConfigDto>(setting.ValueJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (parsed != null && parsed.Rules != null && parsed.Rules.Count > 0)
                    return parsed;
            }
            catch
            {
                // Fallback to default WES
            }
        }

        return GetDefaultWesScaleConfig();
    }

    /// <summary>
    /// [USAS-365] Quy đổi một điểm hệ 10 sang thang 4.0 theo quy tắc.
    /// </summary>
    public static decimal ConvertScoreToGpa4(decimal rawScore, GradeScaleConfigDto config)
    {
        if (rawScore < 0m || rawScore > 10m)
            throw new ArgumentOutOfRangeException(nameof(rawScore), "Điểm số phải nằm trong khoảng từ 0.0 đến 10.0.");

        // Nếu phương thức cấu hình là linear (raw * 0.4 theo SOW v6)
        if (string.Equals(config.Method, "linear_0.4", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Round(rawScore * 0.4m, 2);
        }

        // Mặc định dò theo bảng khoảng điểm (WES Standard Rule)
        foreach (var rule in config.Rules.OrderByDescending(r => r.MinScore))
        {
            if (rawScore >= rule.MinScore && rawScore <= rule.MaxScore)
                return rule.Gpa4;
        }

        // Fallback an toàn
        return Math.Round(rawScore * 0.4m, 2);
    }

    /// <summary>
    /// [USAS-365] Bảng quy đổi mặc định chuẩn WES (World Education Services) cho hệ THPT/ĐH Việt Nam.
    /// </summary>
    public static GradeScaleConfigDto GetDefaultWesScaleConfig()
    {
        return new GradeScaleConfigDto(
            ScaleKey: AcademicConstants.GradeScale10AppSettingKey,
            SourceName: "Chuẩn thẩm định WES (World Education Services - Hoa Kỳ)",
            Method: "table",
            Rules: new List<GradeScaleRuleDto>
            {
                new(9.0m, 10.0m, 4.0m, "A"),
                new(8.5m, 8.99m, 3.7m, "A-"),
                new(8.0m, 8.49m, 3.5m, "B+"),
                new(7.0m, 7.99m, 3.0m, "B"),
                new(6.5m, 6.99m, 2.5m, "C+"),
                new(5.5m, 6.49m, 2.0m, "C"),
                new(5.0m, 5.49m, 1.5m, "D+"),
                new(4.0m, 4.99m, 1.0m, "D"),
                new(0.0m, 3.99m, 0.0m, "F")
            }
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

    private static bool IsAdvancedSubject(string subject)
    {
        var norm = RemoveDiacritics(subject.ToLowerInvariant());
        return norm.Contains("chuyen") || norm.Contains("nang cao") || norm.Contains("ap") || norm.Contains("honors") || norm.Contains("ib");
    }

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
