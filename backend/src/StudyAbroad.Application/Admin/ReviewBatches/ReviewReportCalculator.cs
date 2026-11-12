using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Admin.ReviewBatches;

/// <summary>Kết luận của một câu đã chấm (một câu một kết luận), chỉ có mã và nhãn, không có nội dung chat.</summary>
public record ReviewGradedItem(string Stratum, string LabelVerdict, string? CorrectIntent, string AnswerVerdict, string? Cause);

/// <summary>
/// Một tỷ lệ kèm khoảng tin cậy 95%. Count/Total là số đếm thô trong mẫu đã chấm. Rate, Low, High nằm trong [0, 1].
/// Với nhóm đã chấm hết (Total bằng số câu của nhóm) thì không còn ngẫu nhiên nên Low = High = Rate.
/// </summary>
public record RateEstimate(int Count, int Total, double Rate, double Low, double High);

public record AnswerCounts(int Correct, int Wrong, int MissingData, int OutOfScope);

public record StratumReport(
    string Stratum,
    int Population,
    int Picked,
    int Graded,
    bool IsCensus,
    AnswerCounts Answers,
    RateEstimate? AnswerWrongRate,
    RateEstimate? LabelWrongRate);

public record CauseCount(string Cause, int Count);

/// <summary>Nhãn đúng mà người chấm chọn khi SLM gán sai. "unknown" là người chấm chọn "Không chắc".</summary>
public record CorrectIntentCount(string Intent, int Count);

/// <summary>
/// Báo cáo một đợt. AnswerWrong và LabelWrong là tỷ lệ cho TOÀN BỘ các câu thuộc các nhóm đã có câu chấm (có trọng số theo số câu mỗi nhóm lúc bốc).
/// CoveredPopulation là số câu của các nhóm đó, Population là số câu của mọi nhóm; hai số khác nhau nghĩa là còn nhóm chưa chấm câu nào
/// và tỷ lệ chung chưa nói gì về nhóm đó. Null khi chưa chấm câu nào.
/// </summary>
public record ReviewReport(
    int Graded,
    int Population,
    int CoveredPopulation,
    RateEstimate? AnswerWrong,
    RateEstimate? LabelWrong,
    IReadOnlyList<StratumReport> Strata,
    IReadOnlyList<CauseCount> Causes,
    IReadOnlyList<CorrectIntentCount> CorrectIntents);

/// <summary>
/// Tính báo cáo từ kết luận đã chấm và số câu mỗi nhóm lúc bốc. Hàm thuần, không đọc database.
/// Vì mỗi nhóm bốc số câu bằng nhau (không tỷ lệ theo kích thước nhóm), tỷ lệ chung PHẢI tính có trọng số theo số câu của nhóm,
/// nếu không nhóm nhỏ sẽ bị tính nặng hơn thực tế.
/// </summary>
public static class ReviewReportCalculator
{
    private const double Z = 1.96;   // 95%

    public static ReviewReport Compute(IReadOnlyList<StratumCount> strata, IReadOnlyList<ReviewGradedItem> graded)
    {
        // Số câu mỗi nhóm lấy từ lúc bốc. Nhóm có câu chấm mà không có trong số liệu lúc bốc (dữ liệu lệch) thì lấy số câu đã chấm làm số câu của nhóm
        var population = strata.ToDictionary(s => s.Stratum, s => s, StringComparer.Ordinal);
        foreach (var name in graded.Select(g => g.Stratum).Distinct(StringComparer.Ordinal))
        {
            if (!population.ContainsKey(name))
            {
                var count = graded.Count(g => g.Stratum == name);
                population[name] = new StratumCount(name, count, count);
            }
        }

        var reports = new List<StratumReport>();
        foreach (var stratum in population.Values.OrderBy(s => s.Stratum, StringComparer.Ordinal))
        {
            var items = graded.Where(g => g.Stratum == stratum.Stratum).ToList();
            var n = items.Count;
            var answerWrong = items.Count(i => i.AnswerVerdict == ReviewAnswerVerdicts.Wrong);
            var labelWrong = items.Count(i => i.LabelVerdict == ReviewLabelVerdicts.Wrong);
            var census = n > 0 && n >= stratum.Population;
            reports.Add(new StratumReport(
                stratum.Stratum, stratum.Population, stratum.Picked, n, census,
                new AnswerCounts(
                    items.Count(i => i.AnswerVerdict == ReviewAnswerVerdicts.Correct),
                    answerWrong,
                    items.Count(i => i.AnswerVerdict == ReviewAnswerVerdicts.MissingData),
                    items.Count(i => i.AnswerVerdict == ReviewAnswerVerdicts.OutOfScope)),
                n == 0 ? null : StratumRate(answerWrong, n, census),
                n == 0 ? null : StratumRate(labelWrong, n, census)));
        }

        var causes = graded
            .Where(g => g.AnswerVerdict == ReviewAnswerVerdicts.Wrong && g.Cause is not null)
            .GroupBy(g => g.Cause!, StringComparer.Ordinal)
            .Select(g => new CauseCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count).ThenBy(c => c.Cause, StringComparer.Ordinal)
            .ToList();

        var intents = graded
            .Where(g => g.LabelVerdict == ReviewLabelVerdicts.Wrong)
            .GroupBy(g => g.CorrectIntent ?? "unknown", StringComparer.Ordinal)
            .Select(g => new CorrectIntentCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count).ThenBy(c => c.Intent, StringComparer.Ordinal)
            .ToList();

        var covered = reports.Where(r => r.Graded > 0).ToList();
        return new ReviewReport(
            graded.Count,
            reports.Sum(r => r.Population),
            covered.Sum(r => r.Population),
            Weighted(covered, r => r.AnswerWrongRate),
            Weighted(covered, r => r.LabelWrongRate),
            reports,
            causes,
            intents);
    }

    // Khoảng Wilson cho một nhóm; nhóm đã chấm hết thì là con số đúng, không có khoảng
    private static RateEstimate StratumRate(int count, int total, bool census)
    {
        var rate = (double)count / total;
        if (census)
            return new RateEstimate(count, total, rate, rate, rate);

        var z2 = Z * Z;
        var center = (rate + z2 / (2 * total)) / (1 + z2 / total);
        var half = Z * Math.Sqrt(rate * (1 - rate) / total + z2 / (4.0 * total * total)) / (1 + z2 / total);
        return new RateEstimate(count, total, rate, Math.Max(0, center - half), Math.Min(1, center + half));
    }

    // Ước lượng chung theo tầng: tổng (số câu nhóm / tổng số câu) x tỷ lệ nhóm. Phương sai có hiệu chỉnh quần thể hữu hạn (nhóm chấm hết thì
    // không đóng góp sai số) và làm mượt tỷ lệ (x+1)/(n+2) để nhóm toàn đúng hoặc toàn sai không bị tính là chắc chắn tuyệt đối.
    private static RateEstimate? Weighted(IReadOnlyList<StratumReport> covered, Func<StratumReport, RateEstimate?> pick)
    {
        if (covered.Count == 0)
            return null;

        var total = (double)covered.Sum(r => r.Population);
        double rate = 0, variance = 0;
        var count = 0;
        var graded = 0;
        foreach (var report in covered)
        {
            var estimate = pick(report)!;
            var weight = report.Population / total;
            rate += weight * estimate.Rate;
            count += estimate.Count;
            graded += estimate.Total;

            var n = estimate.Total;
            var smoothed = (estimate.Count + 1.0) / (n + 2.0);
            var finite = Math.Max(0, 1 - (double)n / report.Population);
            variance += weight * weight * smoothed * (1 - smoothed) / n * finite;
        }

        var margin = Z * Math.Sqrt(variance);
        return new RateEstimate(count, graded, rate, Math.Max(0, rate - margin), Math.Min(1, rate + margin));
    }
}
