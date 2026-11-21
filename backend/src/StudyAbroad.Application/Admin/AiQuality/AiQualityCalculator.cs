using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Tính báo cáo chất lượng AI (UC-90) từ danh sách lượt chat. Hàm thuần: không đọc database, không đọc giờ hệ thống,
/// cùng đầu vào luôn ra cùng kết quả. Phần lấy dữ liệu và đổi tin nhắn thành AiQualityTurn làm ở bước khác (xem AiQualityMetaReader).
/// </summary>
public static class AiQualityCalculator
{
    /// <summary>Tên nhóm cho lượt không biết loại ý (ví dụ bấm nút, tin cũ).</summary>
    public const string OtherFeature = "other";

    /// <summary>
    /// Báo cáo cho các lượt có ngày UTC nằm trong [from, to] (cả hai đầu). Lượt ngoài khoảng bị bỏ qua.
    /// Mọi ngày trong khoảng đều có dòng ở ByDay, kể cả ngày không có lượt nào.
    /// </summary>
    public static AiQualityReport Build(IEnumerable<AiQualityTurn> turns, DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException("Ngày kết thúc phải từ ngày bắt đầu trở đi.", nameof(to));

        var inRange = turns.Where(t => DateOnly.FromDateTime(t.CreatedAt) >= from && DateOnly.FromDateTime(t.CreatedAt) <= to).ToList();

        var byDay = inRange.GroupBy(t => DateOnly.FromDateTime(t.CreatedAt)).ToDictionary(g => g.Key, g => g.ToList());
        var days = new List<AiQualityDay>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var list = byDay.TryGetValue(day, out var found) ? found : [];
            days.Add(new AiQualityDay(day, Count(list), Latency(list)));
        }

        var features = inRange
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Feature) ? OtherFeature : t.Feature)
            .Select(g => new AiQualityFeature(g.Key, Count(g), Latency(g)))
            .OrderByDescending(f => f.Counts.Total)
            .ThenBy(f => f.Feature, StringComparer.Ordinal)
            .ToList();

        var failedSteps = inRange
            .SelectMany(t => t.FailedSteps.Distinct(StringComparer.Ordinal))
            .GroupBy(step => step, StringComparer.Ordinal)
            .Select(g => new FailedStepCount(g.Key, g.Count()))
            .OrderByDescending(f => f.Turns)
            .ThenBy(f => f.Step, StringComparer.Ordinal)
            .ToList();

        return new AiQualityReport(
            from, to, Count(inRange), Latency(inRange),
            inRange.Count(t => t.SlmFallback), inRange.Count(t => t.UsedFallback),
            days, features, failedSteps);
    }

    private static OutcomeCounts Count(IEnumerable<AiQualityTurn> turns)
    {
        int answered = 0, noAnswer = 0, refused = 0, failed = 0, unclassified = 0;
        foreach (var turn in turns)
        {
            switch (turn.Outcome)
            {
                case ChatOutcomes.Answered: answered++; break;
                case ChatOutcomes.NoAnswer: noAnswer++; break;
                case ChatOutcomes.Refused: refused++; break;
                case ChatOutcomes.Failed: failed++; break;
                default: unclassified++; break;   // null hoặc mã lạ: không bỏ đi để tổng vẫn khớp
            }
        }
        return new OutcomeCounts(answered + noAnswer + refused + failed + unclassified, answered, noAnswer, refused, failed, unclassified);
    }

    private static LatencySummary Latency(IEnumerable<AiQualityTurn> turns)
    {
        var sorted = turns.Where(t => t.TurnMs is >= 0).Select(t => t.TurnMs!.Value).Order().ToList();
        if (sorted.Count == 0)
            return new LatencySummary(0, null, null, null);
        return new LatencySummary(sorted.Count, Math.Round(sorted.Average(), 1), Percentile(sorted, 0.50), Percentile(sorted, 0.95));
    }

    // Thứ hạng gần nhất: phần tử thứ ceil(p × n), đếm từ 1; danh sách đã sắp tăng dần và không rỗng
    private static long Percentile(IReadOnlyList<long> sorted, double p)
    {
        var rank = (int)Math.Ceiling(p * sorted.Count);
        return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
    }
}
