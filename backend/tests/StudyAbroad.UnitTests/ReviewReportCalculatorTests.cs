using StudyAbroad.Application.Admin.ReviewBatches;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

// Tính báo cáo từ kết luận đã chấm (tab 3, bước 3a): tỷ lệ có trọng số theo nhóm, khoảng tin cậy, nguyên nhân, nhãn đúng.
public class ReviewReportCalculatorTests
{
    private static ReviewGradedItem Ok(string stratum) =>
        new(stratum, ReviewLabelVerdicts.Correct, null, ReviewAnswerVerdicts.Correct, null);

    private static ReviewGradedItem WrongAnswer(string stratum, string cause = ReviewCauses.Hallucination) =>
        new(stratum, ReviewLabelVerdicts.Correct, null, ReviewAnswerVerdicts.Wrong, cause);

    private static List<ReviewGradedItem> Many(string stratum, int wrong, int total) =>
        Enumerable.Range(0, total).Select(i => i < wrong ? WrongAnswer(stratum) : Ok(stratum)).ToList();

    [Fact]
    public void OverallRate_IsWeightedByStratumPopulation_NotAverageOfRates()
    {
        // Ví dụ trong lúc giải thích: 400/1000/100 câu, chấm 20 mỗi nhóm, sai 2/6/1.
        // Trung bình cộng thẳng sẽ ra 15%, đúng phải là (400x10% + 1000x30% + 100x5%) / 1500 = 23%.
        var strata = new List<StratumCount> { new("scenario", 400, 20), new("knowledge", 1000, 20), new("profile_review", 100, 20) };
        var graded = Many("scenario", 2, 20).Concat(Many("knowledge", 6, 20)).Concat(Many("profile_review", 1, 20)).ToList();

        var report = ReviewReportCalculator.Compute(strata, graded);

        Assert.Equal(0.23, report.AnswerWrong!.Rate, 6);
        Assert.Equal(9, report.AnswerWrong.Count);
        Assert.Equal(60, report.AnswerWrong.Total);
        Assert.Equal(1500, report.Population);
        Assert.Equal(1500, report.CoveredPopulation);
    }

    [Fact]
    public void OverallInterval_ContainsTheRate_AndStaysInsideZeroAndOne()
    {
        var strata = new List<StratumCount> { new("scenario", 400, 20), new("knowledge", 1000, 20) };
        var graded = Many("scenario", 2, 20).Concat(Many("knowledge", 6, 20)).ToList();

        var overall = ReviewReportCalculator.Compute(strata, graded).AnswerWrong!;

        Assert.InRange(overall.Rate, overall.Low, overall.High);
        Assert.True(overall.Low >= 0 && overall.High <= 1);
        Assert.True(overall.High - overall.Low > 0.05, "mẫu 20 câu mỗi nhóm thì khoảng phải còn rộng, không được giả vờ chắc chắn");
    }

    [Fact]
    public void StratumInterval_Wilson_IsWideForSmallSamples()
    {
        var strata = new List<StratumCount> { new("knowledge", 1000, 20) };

        var stratum = ReviewReportCalculator.Compute(strata, Many("knowledge", 6, 20)).Strata.Single();

        Assert.Equal(0.3, stratum.AnswerWrongRate!.Rate, 6);
        Assert.True(stratum.AnswerWrongRate.Low < 0.15);
        Assert.True(stratum.AnswerWrongRate.High > 0.5);
        Assert.False(stratum.IsCensus);
    }

    [Fact]
    public void AllWrongOrAllRight_StillGetsAnInterval_NotCertainty()
    {
        var strata = new List<StratumCount> { new("knowledge", 1000, 20) };

        var none = ReviewReportCalculator.Compute(strata, Many("knowledge", 0, 20)).Strata.Single().AnswerWrongRate!;

        Assert.Equal(0, none.Rate);
        Assert.True(none.High > 0.05, "0 lỗi trong 20 câu không có nghĩa là không bao giờ lỗi");
    }

    [Fact]
    public void StratumGradedEntirely_IsExact_WithNoInterval()
    {
        // Đợt thử: mỗi nhóm có ít câu hơn số bốc nên lấy hết, đây là kiểm tra toàn bộ chứ không phải mẫu
        var strata = new List<StratumCount> { new("scenario", 2, 2), new("unlabeled", 2, 2) };
        var graded = new List<ReviewGradedItem> { WrongAnswer("scenario"), Ok("scenario"), Ok("unlabeled"), Ok("unlabeled") };

        var report = ReviewReportCalculator.Compute(strata, graded);

        var scenario = report.Strata.Single(s => s.Stratum == "scenario");
        Assert.True(scenario.IsCensus);
        Assert.Equal(scenario.AnswerWrongRate!.Rate, scenario.AnswerWrongRate.Low);
        Assert.Equal(scenario.AnswerWrongRate.Rate, scenario.AnswerWrongRate.High);
        Assert.Equal(0.25, report.AnswerWrong!.Rate, 6);
        Assert.Equal(report.AnswerWrong.Rate, report.AnswerWrong.Low);
        Assert.Equal(report.AnswerWrong.Rate, report.AnswerWrong.High);
    }

    [Fact]
    public void StratumWithNoGradedItems_IsReportedButExcludedFromTheOverallRate()
    {
        var strata = new List<StratumCount> { new("scenario", 100, 10), new("knowledge", 900, 10) };

        var report = ReviewReportCalculator.Compute(strata, Many("scenario", 5, 10));

        Assert.Equal(1000, report.Population);
        Assert.Equal(100, report.CoveredPopulation);   // tỷ lệ chung mới nói về nhóm scenario, chưa nói về knowledge
        var knowledge = report.Strata.Single(s => s.Stratum == "knowledge");
        Assert.Equal(0, knowledge.Graded);
        Assert.Null(knowledge.AnswerWrongRate);
        Assert.Equal(0.5, report.AnswerWrong!.Rate, 6);
    }

    [Fact]
    public void NothingGraded_GivesNoRates()
    {
        var report = ReviewReportCalculator.Compute([new StratumCount("scenario", 10, 5)], []);

        Assert.Equal(0, report.Graded);
        Assert.Null(report.AnswerWrong);
        Assert.Null(report.LabelWrong);
        Assert.Empty(report.Causes);
        Assert.Equal(10, report.Population);
        Assert.Equal(0, report.CoveredPopulation);
    }

    [Fact]
    public void AnswerCounts_SplitByVerdict()
    {
        var strata = new List<StratumCount> { new("knowledge", 4, 4) };
        var graded = new List<ReviewGradedItem>
        {
            Ok("knowledge"), WrongAnswer("knowledge"),
            new("knowledge", ReviewLabelVerdicts.Correct, null, ReviewAnswerVerdicts.MissingData, null),
            new("knowledge", ReviewLabelVerdicts.Correct, null, ReviewAnswerVerdicts.OutOfScope, null),
        };

        var counts = ReviewReportCalculator.Compute(strata, graded).Strata.Single().Answers;

        Assert.Equal(new AnswerCounts(1, 1, 1, 1), counts);
    }

    [Fact]
    public void Causes_AreCountedOnlyForWrongAnswers_MostCommonFirst()
    {
        var strata = new List<StratumCount> { new("knowledge", 10, 5) };
        var graded = new List<ReviewGradedItem>
        {
            WrongAnswer("knowledge", ReviewCauses.MissingKnowledge),
            WrongAnswer("knowledge", ReviewCauses.MissingKnowledge),
            WrongAnswer("knowledge", ReviewCauses.Hallucination),
            Ok("knowledge"), Ok("knowledge"),
        };

        var causes = ReviewReportCalculator.Compute(strata, graded).Causes;

        Assert.Equal([new CauseCount(ReviewCauses.MissingKnowledge, 2), new CauseCount(ReviewCauses.Hallucination, 1)], causes);
    }

    [Fact]
    public void LabelWrong_CountsCorrectIntents_AndUnknownWhenTheReviewerWasNotSure()
    {
        var strata = new List<StratumCount> { new("scenario", 10, 4) };
        var graded = new List<ReviewGradedItem>
        {
            new("scenario", ReviewLabelVerdicts.Wrong, "knowledge", ReviewAnswerVerdicts.Wrong, ReviewCauses.MisreadIntent),
            new("scenario", ReviewLabelVerdicts.Wrong, "knowledge", ReviewAnswerVerdicts.Correct, null),
            new("scenario", ReviewLabelVerdicts.Wrong, null, ReviewAnswerVerdicts.Correct, null),
            Ok("scenario"),
        };

        var report = ReviewReportCalculator.Compute(strata, graded);

        Assert.Equal([new CorrectIntentCount("knowledge", 2), new CorrectIntentCount("unknown", 1)], report.CorrectIntents);
        Assert.Equal(0.75, report.LabelWrong!.Rate, 6);
    }

    [Fact]
    public void GradedItemInAStratumMissingFromTheDrawRecord_IsTreatedAsFullyGraded()
    {
        // JSON số nhóm hỏng hoặc thiếu thì không được làm sập báo cáo
        var report = ReviewReportCalculator.Compute([], [Ok("scenario"), WrongAnswer("scenario")]);

        var stratum = report.Strata.Single();
        Assert.Equal(2, stratum.Population);
        Assert.True(stratum.IsCensus);
        Assert.Equal(0.5, report.AnswerWrong!.Rate, 6);
    }
}
