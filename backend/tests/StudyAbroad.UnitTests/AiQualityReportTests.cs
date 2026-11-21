using StudyAbroad.Application.Admin.AiQuality;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

// UC-90: tính báo cáo chất lượng AI từ danh sách lượt chat. Hàm thuần nên test bằng dữ liệu dựng tay.
public class AiQualityReportTests
{
    private static readonly DateOnly D1 = new(2026, 11, 1);
    private static readonly DateOnly D3 = new(2026, 11, 3);

    private static DateTime At(int day, int hour = 10) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    private static AiQualityTurn Turn(
        string? outcome, int day = 1, long? ms = 1000, string? feature = "knowledge", string[]? failed = null,
        bool slmFallback = false, bool usedFallback = false) =>
        new(At(day), outcome, ms, feature, failed ?? [], slmFallback, usedFallback);

    private static AiUsageDto Call(string source, string purpose, bool ok = true) =>
        new(source, "m", purpose, "1.0", ok ? 100 : 0, ok ? 10 : 0, 0, ok, ok ? null : "timeout", 100);

    // ---- đếm bốn loại

    [Fact]
    public void CountsEachOutcome_AndTheRatesAddUpToOneHundredPercent()
    {
        var turns = new[]
        {
            Turn("answered"), Turn("answered"), Turn("answered"), Turn("answered"), Turn("answered"), Turn("answered"),
            Turn("no_answer"), Turn("no_answer"), Turn("refused"), Turn("failed"),
        };

        var total = AiQualityCalculator.Build(turns, D1, D3).Total;

        Assert.Equal((10, 6, 2, 1, 1, 0), (total.Total, total.Answered, total.NoAnswer, total.Refused, total.Failed, total.Unclassified));
        Assert.Equal((0.6, 0.2, 0.1, 0.1), (total.AnsweredRate, total.NoAnswerRate, total.RefusedRate, total.FailedRate));
        Assert.Equal(1.0, total.AnsweredRate + total.NoAnswerRate + total.RefusedRate + total.FailedRate + total.UnclassifiedRate, 10);
    }

    [Fact]
    public void RowsWithoutAKnownOutcome_AreUnclassified_NotDropped()
    {
        // Tin cũ chưa có outcome và mã lạ: vẫn nằm trong tổng để các tỉ lệ khớp nhau
        var turns = new[] { Turn("answered"), Turn(null), Turn("something_new") };

        var total = AiQualityCalculator.Build(turns, D1, D3).Total;

        Assert.Equal((3, 1, 2), (total.Total, total.Answered, total.Unclassified));
        Assert.Equal(1.0, total.AnsweredRate + total.UnclassifiedRate, 10);
    }

    [Fact]
    public void NoTurnsAtAll_GivesZeroRatesInsteadOfDividingByZero()
    {
        var report = AiQualityCalculator.Build([], D1, D3);

        Assert.Equal(0, report.Total.Total);
        Assert.Equal(0, report.Total.AnsweredRate);
        Assert.Equal(0, report.Total.FailedRate);
        Assert.Equal((0, null, null, null), (report.Latency.Measured, report.Latency.AvgMs, report.Latency.P50Ms, report.Latency.P95Ms));
        Assert.Empty(report.ByFeature);
        Assert.Empty(report.FailedSteps);
    }

    // ---- thời gian phản hồi

    [Fact]
    public void Latency_UsesTheNearestRankPercentile()
    {
        // 1 đến 100 ms: p50 là phần tử thứ 50, p95 là phần tử thứ 95
        var turns = Enumerable.Range(1, 100).Select(i => Turn("answered", ms: i)).ToList();

        var latency = AiQualityCalculator.Build(turns, D1, D3).Latency;

        Assert.Equal((100, 50.5, 50L, 95L), (latency.Measured, latency.AvgMs, latency.P50Ms, latency.P95Ms));
    }

    [Fact]
    public void Latency_OfASingleTurn_IsThatTurn()
    {
        var latency = AiQualityCalculator.Build([Turn("answered", ms: 4200)], D1, D3).Latency;

        Assert.Equal((1, 4200.0, 4200L, 4200L), (latency.Measured, latency.AvgMs, latency.P50Ms, latency.P95Ms));
    }

    [Fact]
    public void Latency_IgnoresTurnsThatWereNotMeasured_ButStillCountsThem()
    {
        var turns = new[] { Turn("answered", ms: 1000), Turn("answered", ms: 3000), Turn("answered", ms: null) };

        var report = AiQualityCalculator.Build(turns, D1, D3);

        Assert.Equal(3, report.Total.Total);          // vẫn là một lượt trong số đếm
        Assert.Equal((2, 2000.0), (report.Latency.Measured, report.Latency.AvgMs));
    }

    [Fact]
    public void AverageHidesASlowTail_ButP95ShowsIt()
    {
        // 95 lượt 3 giây và 5 lượt 60 giây: trung bình 5,85 giây nghe ổn, p95 cho thấy 5% người dùng chờ cả phút
        var turns = Enumerable.Repeat(Turn("answered", ms: 3000), 95).Concat(Enumerable.Repeat(Turn("answered", ms: 60_000), 5)).ToList();

        var latency = AiQualityCalculator.Build(turns, D1, D3).Latency;

        Assert.Equal(5850.0, latency.AvgMs);
        Assert.Equal((3000L, 3000L), (latency.P50Ms, latency.P95Ms));   // thứ 95 vẫn là 3 giây
        var worse = AiQualityCalculator.Build(turns.Append(Turn("answered", ms: 60_000)), D1, D3).Latency;
        Assert.Equal(60_000L, worse.P95Ms);                              // thêm một lượt chậm là p95 nhảy lên 60 giây
    }

    // ---- theo ngày

    [Fact]
    public void ByDay_HasARowForEveryDay_EvenWhenNothingHappened()
    {
        var turns = new[] { Turn("answered", day: 1), Turn("failed", day: 3) };

        var days = AiQualityCalculator.Build(turns, D1, D3).ByDay;

        Assert.Equal([D1, new DateOnly(2026, 11, 2), D3], days.Select(d => d.Date));
        Assert.Equal([1, 0, 1], days.Select(d => d.Counts.Total));
        Assert.Equal(0, days[1].Counts.AnsweredRate);
        Assert.Null(days[1].Latency.P95Ms);
    }

    [Fact]
    public void ByDay_UsesTheUtcDate_AndTurnsOutsideTheRangeAreIgnored()
    {
        var turns = new[]
        {
            new AiQualityTurn(new DateTime(2026, 11, 1, 23, 59, 0, DateTimeKind.Utc), "answered", 1, "knowledge", [], false, false),
            new AiQualityTurn(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc), "answered", 1, "knowledge", [], false, false),
            new AiQualityTurn(new DateTime(2026, 10, 31, 23, 59, 0, DateTimeKind.Utc), "answered", 1, "knowledge", [], false, false),   // trước khoảng
            new AiQualityTurn(new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc), "answered", 1, "knowledge", [], false, false),      // sau khoảng
        };

        var report = AiQualityCalculator.Build(turns, D1, D3);

        Assert.Equal(2, report.Total.Total);
        Assert.Equal([1, 1, 0], report.ByDay.Select(d => d.Counts.Total));
    }

    [Fact]
    public void TheDaysAddUpToTheTotal()
    {
        var turns = new[] { Turn("answered", day: 1), Turn("no_answer", day: 1), Turn("refused", day: 2), Turn("failed", day: 3), Turn(null, day: 3) };

        var report = AiQualityCalculator.Build(turns, D1, D3);

        Assert.Equal(report.Total.Total, report.ByDay.Sum(d => d.Counts.Total));
        Assert.Equal(report.Total.Failed, report.ByDay.Sum(d => d.Counts.Failed));
    }

    [Fact]
    public void ARangeEndingBeforeItStarts_IsRejected() =>
        Assert.Throws<ArgumentException>(() => AiQualityCalculator.Build([], D3, D1));

    // ---- theo tính năng, bước hỏng, dự phòng

    [Fact]
    public void ByFeature_GroupsByTheFirstIntent_BiggestFirst_AndUnknownGoesToOther()
    {
        var turns = new[]
        {
            Turn("answered", feature: "knowledge"), Turn("no_answer", feature: "knowledge"), Turn("answered", feature: "knowledge"),
            Turn("answered", feature: "profile_review"),
            Turn("answered", feature: null), Turn("answered", feature: "  "),
        };

        var features = AiQualityCalculator.Build(turns, D1, D3).ByFeature;

        Assert.Equal(["knowledge", "other", "profile_review"], features.Select(f => f.Feature));
        Assert.Equal([3, 2, 1], features.Select(f => f.Counts.Total));
        Assert.Equal((2, 1), (features[0].Counts.Answered, features[0].Counts.NoAnswer));
    }

    [Fact]
    public void FailedSteps_CountTurns_NotCalls_AndEachStepOncePerTurn()
    {
        var turns = new[]
        {
            Turn("failed", failed: ["slm_intents"]),
            Turn("failed", failed: ["slm_intents", "slm_intents", "skill_tra_loi"]),   // trùng trong một lượt chỉ đếm một
            Turn("answered"),
        };

        var steps = AiQualityCalculator.Build(turns, D1, D3).FailedSteps;

        Assert.Equal([("slm_intents", 2), ("skill_tra_loi", 1)], steps.Select(s => (s.Step, s.Turns)));
    }

    [Fact]
    public void FallbackTurns_AreCountedSeparatelyForTheSlmAndForTheCalls()
    {
        var turns = new[] { Turn("answered", slmFallback: true), Turn("answered", usedFallback: true), Turn("answered", slmFallback: true, usedFallback: true), Turn("answered") };

        var report = AiQualityCalculator.Build(turns, D1, D3);

        Assert.Equal((2, 2), (report.SlmFallbackTurns, report.FallbackTurns));
    }

    // ---- đọc meta_json: khớp với những gì ChatTurn ghi

    [Fact]
    public void MetaReader_ReadsWhatChatTurnWrites()
    {
        var usage = new[] { Call("ollama", "slm_intents", ok: false), Call("ollama", "slm_intents", ok: false) };
        var reply = ChatTurn.ReplyMeta(usage, "school_info", ["knowledge", "collect_profile"], 7300);
        var question = ChatTurn.UserMeta(new AdvisorSlmDto(Intents: ["knowledge"], Confidence: 0, Fallback: true));

        var turn = AiQualityMetaReader.ToTurn(At(1), "failed", reply, question, usedFallback: false);

        // SLM hỏng thì loại ý chạy là nhãn dự phòng của hệ thống, nên tính năng là nhóm riêng chứ không phải "knowledge"
        Assert.Equal((7300L, AiQualityMetaReader.SlmFallbackFeature, true), (turn.TurnMs, turn.Feature, turn.SlmFallback));
        Assert.Equal(["slm_intents"], turn.FailedSteps);
        Assert.Equal("failed", turn.Outcome);
    }

    [Fact]
    public void MetaReader_ASlmFallbackTurnIsItsOwnFeature_NotTheOutOfScopeLabelTheSystemSubstituted()
    {
        // Model hỏng hai lần: hệ thống gán dự phòng "ngoài phạm vi, mơ hồ". Câu có thể là yêu cầu hợp lệ (xin gặp người thật), nên không
        // được gom chung với câu mà model thật sự gán là ngoài phạm vi
        var reply = ChatTurn.ReplyMeta([], null, ["out_of_scope"], 3500);
        var fallbackQuestion = ChatTurn.UserMeta(new AdvisorSlmDto(Intents: ["out_of_scope"], OutOfScope: "ambiguous", Confidence: 0, Fallback: true));
        var realQuestion = ChatTurn.UserMeta(new AdvisorSlmDto(Intents: ["out_of_scope"], OutOfScope: "exception", Confidence: 0.9, Fallback: false));

        Assert.Equal(AiQualityMetaReader.SlmFallbackFeature, AiQualityMetaReader.ToTurn(At(1), "no_answer", reply, fallbackQuestion, false).Feature);
        Assert.Equal("out_of_scope", AiQualityMetaReader.ToTurn(At(1), "refused", reply, realQuestion, false).Feature);
    }

    [Fact]
    public void MetaReader_ToleratesMissingOrBrokenJson_WithoutThrowing()
    {
        foreach (var bad in new string?[] { null, "", "   ", "{not json", "[1,2]", "\"text\"", "{\"turnMs\":\"abc\",\"skills\":5,\"failedSteps\":{}}" })
        {
            var turn = AiQualityMetaReader.ToTurn(At(1), "answered", bad, bad, usedFallback: false);

            Assert.Null(turn.TurnMs);
            Assert.Null(turn.Feature);
            Assert.Empty(turn.FailedSteps);
            Assert.False(turn.SlmFallback);
        }
    }
}
