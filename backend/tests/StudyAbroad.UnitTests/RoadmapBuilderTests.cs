using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Dashboard;
using StudyAbroad.Application.Dashboard.Roadmap;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>Luật tính lộ trình theo trường mục tiêu (kế hoạch mục 6.1–6.3).</summary>
public class RoadmapBuilderTests
{
    private static readonly DateOnly Today = new(2026, 11, 26);
    private static readonly Guid SchoolA = Guid.NewGuid();
    private static readonly Guid SchoolB = Guid.NewGuid();

    private static TargetSchoolDto Target(Guid id, string name, string status = "considering", string? round = null,
        DateTime? deadline = null, int priority = 0) =>
        new(Guid.NewGuid(), Guid.NewGuid(), id, name, name, null, null, null, null, null, "match", status, priority,
            null, round, deadline, DateTime.UtcNow, null);

    private static ProfileCertification Cert(string type, decimal? score, bool target = false, DateOnly? date = null) =>
        new() { TestType = type, Category = type == "sat" ? "standardized" : "english", OverallScore = score, IsTarget = target, TestDate = date };

    private static RoadmapOfferingRow Offering(Guid id, decimal? gpa = 3.6m, decimal? ielts = 6.5m, int? sat25 = null, int? sat75 = null,
        string? deadlines = null, string? satPolicy = "optional") =>
        new(id, gpa, ielts, 80m, 110m, sat25, sat75, satPolicy, deadlines);

    private static RoadmapDto Build(StudentProfile profile, List<ProfileCertification> certs, List<TargetSchoolDto> targets,
        Dictionary<Guid, RoadmapOfferingRow>? offerings = null, Dictionary<Guid, UniversityCostRow>? costs = null,
        List<RoadmapScholarshipRow>? scholarships = null, RoadmapAverageMarks? average = null) =>
        RoadmapBuilder.Build(new RoadmapInputs(profile, certs, targets,
            offerings ?? [], costs ?? [], scholarships ?? [], average), Today);

    // ---------- Mức sẵn sàng ----------

    [Theory]
    [InlineData(3.8, 3.6, "met")]
    [InlineData(3.6, 3.6, "met")]
    [InlineData(3.5, 3.6, "near")]
    [InlineData(3.4, 3.6, "below")]
    public void Gpa_ComparesWithAvgGpa4_NearWithin01(double mine, double mark, string expected)
    {
        Assert.Equal(expected, RoadmapBuilder.GpaCriterion((decimal)mine, (decimal)mark).Status);
    }

    [Fact]
    public void Gpa_MissingEitherSide_IsUnknown()
    {
        Assert.Equal("unknown", RoadmapBuilder.GpaCriterion(null, 3.6m).Status);
        Assert.Equal("unknown", RoadmapBuilder.GpaCriterion(3.6m, null).Status);
    }

    [Fact]
    public void English_ComparesSameCertificateType_NoConversion()
    {
        var scores = RoadmapBuilder.RealScores([Cert("toefl", 85)]);
        var c = RoadmapBuilder.EnglishCriterion(scores, [], Offering(SchoolA));

        Assert.Equal("toefl", c.Type);
        Assert.Equal(80m, c.Mark);
        Assert.Equal("met", c.Status);
    }

    [Fact]
    public void English_TargetScoreIsNotARealScore()
    {
        var certs = new List<ProfileCertification> { Cert("ielts", 7.0m, target: true) };
        var c = RoadmapBuilder.EnglishCriterion(RoadmapBuilder.RealScores(certs), certs, Offering(SchoolA));

        Assert.Equal("ielts", c.Type);
        Assert.Null(c.Mine);
        Assert.Equal("unknown", c.Status);
    }

    [Theory]
    [InlineData(1400, "met")]
    [InlineData(1300, "near")]
    [InlineData(1100, "below")]
    public void Sat_MarkIsMidpoint_NearFromSat25(int score, string expected)
    {
        var scores = RoadmapBuilder.RealScores([Cert("sat", score)]);
        var c = RoadmapBuilder.SatCriterion(scores, [], Offering(SchoolA, sat25: 1200, sat75: 1500));

        Assert.Equal(1350m, c!.Mark);
        Assert.Equal(expected, c.Status);
    }

    [Fact]
    public void Sat_SkippedWhenStudentHasNoSatOrSchoolDoesNotAccept()
    {
        Assert.Null(RoadmapBuilder.SatCriterion(RoadmapBuilder.RealScores([]), [], Offering(SchoolA, sat25: 1200, sat75: 1500)));
        var scores = RoadmapBuilder.RealScores([Cert("sat", 1400)]);
        Assert.Null(RoadmapBuilder.SatCriterion(scores, [], Offering(SchoolA, sat25: 1200, sat75: 1500, satPolicy: "not_accepted")));
    }

    [Fact]
    public void Budget_ComparesTotalCostWithBudgetMax()
    {
        Assert.Equal("met", RoadmapBuilder.BudgetCriterion(40000, 50000).Status);
        Assert.Equal("below", RoadmapBuilder.BudgetCriterion(60000, 50000).Status);
        Assert.Equal("unknown", RoadmapBuilder.BudgetCriterion(null, 50000).Status);
    }

    [Fact]
    public void Scholarship_EligibleOnlyWhenAllListedMinimumsMet()
    {
        var s = new RoadmapScholarshipRow(Guid.NewGuid(), SchoolA, "Merit", 3.5m, 6.5m, null, null, null, null);
        var good = RoadmapBuilder.RealScores([Cert("ielts", 7)]);

        Assert.True(RoadmapBuilder.ScholarshipEligible(s, 3.6m, good));
        Assert.False(RoadmapBuilder.ScholarshipEligible(s, 3.4m, good));
        Assert.False(RoadmapBuilder.ScholarshipEligible(s, 3.6m, RoadmapBuilder.RealScores([])));
    }

    [Fact]
    public void TargetMode_SummaryCountsSchoolsWhereAllKnownCriteriaMet()
    {
        var profile = new StudentProfile { OverallGpa = 3.7m, TargetIntake = "Fall 2028", BudgetMaxUsd = 50000 };
        var certs = new List<ProfileCertification> { Cert("ielts", 7) };
        var offerings = new Dictionary<Guid, RoadmapOfferingRow>
        {
            [SchoolA] = Offering(SchoolA, gpa: 3.6m),
            [SchoolB] = Offering(SchoolB, gpa: 3.9m),
        };

        var r = Build(profile, certs, [Target(SchoolA, "A"), Target(SchoolB, "B")], offerings);

        Assert.Equal("target", r.Mode);
        Assert.Equal("Đạt đủ điều kiện 1/2 trường mục tiêu", r.Summary);
        var a = r.Schools.Single(s => s.UniversityId == SchoolA);
        Assert.Equal(2, a.MetCount);
        Assert.Equal(2, a.KnownCount); // budget unknown: chưa có chi phí
    }

    [Fact]
    public void TargetMode_TakesAtMostFiveSchools()
    {
        var targets = Enumerable.Range(0, 7).Select(i => Target(Guid.NewGuid(), $"S{i}")).ToList();
        var r = Build(new StudentProfile(), [], targets);
        Assert.Equal(5, r.Schools.Count);
    }

    [Fact]
    public void AverageMode_UsesBenchmarksAndAddsChooseTargetTask()
    {
        var avg = RoadmapService.ToAverage(new JsonObject { ["gpa4"] = 3.6m, ["english"] = new JsonObject { ["ielts"] = 6.5m } });
        var r = Build(new StudentProfile { OverallGpa = 3.7m }, [Cert("ielts", 6)], [], average: avg);

        Assert.Equal("average", r.Mode);
        var row = Assert.Single(r.Schools);
        Assert.Null(row.UniversityId);
        Assert.Equal("met", row.Criteria.Single(c => c.Key == "gpa").Status);
        Assert.Equal("below", row.Criteria.Single(c => c.Key == "english").Status);
        Assert.Contains(r.Tasks, t => t.Id == "target:choose" && t.Status == "todo");
        Assert.True(r.Milestones.Single(m => m.Kind == "deadline").IsEstimate);
    }

    // ---------- Mốc ----------

    [Fact]
    public void Deadline_PrefersTargetSchoolDeadline()
    {
        var t = Target(SchoolA, "A", round: "Early Decision", deadline: new DateTime(2027, 11, 1));
        var (date, round, estimate) = RoadmapBuilder.ResolveDeadline(t, "{\"RD\":\"2026-01-05\"}", "Fall", 2028);

        Assert.Equal(new DateOnly(2027, 11, 1), date);
        Assert.Equal("ED", round);
        Assert.False(estimate);
    }

    [Fact]
    public void Deadline_FromDeadlinesJson_ShiftsYearToIntake()
    {
        var json = "{\"ED\":\"2025-11-01\",\"RD\":\"2026-01-05\"}";

        var rd = RoadmapBuilder.ResolveDeadline(Target(SchoolA, "A"), json, "Fall", 2028);
        Assert.Equal(new DateOnly(2028, 1, 5), rd.Date);
        Assert.Equal("RD", rd.Round);

        var ed = RoadmapBuilder.ResolveDeadline(Target(SchoolA, "A", round: "ED"), json, "Fall", 2028);
        Assert.Equal(new DateOnly(2027, 11, 1), ed.Date);
        Assert.False(ed.Estimate);
    }

    [Fact]
    public void Deadline_NoData_IsEstimate()
    {
        var rd = RoadmapBuilder.ResolveDeadline(Target(SchoolA, "A"), null, "Fall", 2028);
        Assert.Equal(new DateOnly(2028, 1, 1), rd.Date);
        Assert.True(rd.Estimate);

        var ea = RoadmapBuilder.ResolveDeadline(Target(SchoolA, "A", round: "Early Action"), "not json", "Fall", 2028);
        Assert.Equal(new DateOnly(2027, 11, 1), ea.Date);
    }

    [Fact]
    public void Intake_MissingFallsBackToNextFallAsEstimate()
    {
        Assert.Equal(("Spring", 2028, false), RoadmapBuilder.ParseIntake("spring 2028", Today));
        Assert.Equal(("Fall", 2027, true), RoadmapBuilder.ParseIntake(null, Today));
    }

    [Fact]
    public void Milestones_TestTwoMonthsBeforeEarliestDeadline_VisaThreeMonthsBeforeIntake()
    {
        var targets = new List<TargetSchoolDto>
        {
            Target(SchoolA, "A", deadline: new DateTime(2028, 1, 1)),
            Target(SchoolB, "B", deadline: new DateTime(2027, 11, 1)),
        };
        var offerings = new Dictionary<Guid, RoadmapOfferingRow> { [SchoolA] = Offering(SchoolA), [SchoolB] = Offering(SchoolB) };

        var r = Build(new StudentProfile { TargetIntake = "Fall 2028" }, [], targets, offerings);

        Assert.Equal(new DateOnly(2027, 9, 1), r.Milestones.Single(m => m.Kind == "english_test").Date);
        Assert.Equal(new DateOnly(2028, 5, 1), r.Milestones.Single(m => m.Kind == "visa").Date);
        Assert.Equal(new DateOnly(2028, 8, 1), r.Milestones.Single(m => m.Kind == "intake").Date);
        Assert.Equal(r.Milestones.OrderBy(m => m.Date).Select(m => m.Date), r.Milestones.Select(m => m.Date));
    }

    [Fact]
    public void Milestones_UsePlannedTestDateWhenSet()
    {
        var certs = new List<ProfileCertification> { Cert("ielts", 7, target: true, date: new DateOnly(2027, 3, 1)) };
        var r = Build(new StudentProfile { TargetIntake = "Fall 2028" }, certs, [Target(SchoolA, "A")],
            new() { [SchoolA] = Offering(SchoolA) });

        var m = r.Milestones.Single(x => x.Kind == "english_test");
        Assert.Equal(new DateOnly(2027, 3, 1), m.Date);
        Assert.False(m.IsEstimate);
        Assert.Equal("done", r.Tasks.Single(t => t.Id == "register:ielts").Status);
    }

    // ---------- Nhiệm vụ ----------

    [Fact]
    public void Tasks_EnglishGoalIsHighestMark_AutoDoneWhenRealScoreReaches()
    {
        var offerings = new Dictionary<Guid, RoadmapOfferingRow>
        {
            [SchoolA] = Offering(SchoolA, ielts: 6.5m),
            [SchoolB] = Offering(SchoolB, ielts: 7.0m),
        };
        var targets = new List<TargetSchoolDto> { Target(SchoolA, "A"), Target(SchoolB, "B") };
        var profile = new StudentProfile { TargetIntake = "Fall 2028" };

        var low = Build(profile, [Cert("ielts", 6.5m)], targets, offerings);
        var task = low.Tasks.Single(t => t.Id == "english:ielts");
        Assert.Equal("Đạt IELTS 7", task.Label);
        Assert.Equal("todo", task.Status);
        Assert.Equal(["LUYEN_THI_IELTS"], task.Services);
        Assert.DoesNotContain(low.Tasks, t => t.Id == "register:ielts"); // đã có điểm thật thì không cần đăng ký

        var high = Build(profile, [Cert("ielts", 7.5m)], targets, offerings);
        Assert.Equal("done", high.Tasks.Single(t => t.Id == "english:ielts").Status);
        Assert.DoesNotContain(high.Milestones, m => m.Kind == "english_test");
    }

    [Fact]
    public void Tasks_ApplyDoneBySubmittedStatus_OverdueWhenPast()
    {
        var targets = new List<TargetSchoolDto>
        {
            Target(SchoolA, "A", status: "submitted", deadline: new DateTime(2026, 11, 1)),
            Target(SchoolB, "B", deadline: new DateTime(2026, 11, 1)),
        };

        var r = Build(new StudentProfile { TargetIntake = "Fall 2027" }, [], targets);

        Assert.Equal("done", r.Tasks.Single(t => t.Id == $"apply:{SchoolA}").Status);
        Assert.Equal("overdue", r.Tasks.Single(t => t.Id == $"apply:{SchoolB}").Status);
    }

    [Fact]
    public void Tasks_NoDataTasksAreNotCheckable_AndNotCountedInProgress()
    {
        var targets = new List<TargetSchoolDto> { Target(SchoolA, "A", status: "submitted") };

        var r = Build(new StudentProfile { TargetIntake = "Fall 2028" }, [], targets);

        Assert.False(r.Tasks.Single(t => t.Id == "essay").Checkable);
        Assert.False(r.Tasks.Single(t => t.Id == "visa").Checkable);
        Assert.Equal(r.Tasks.Count(t => t.Checkable), r.Progress.Total);
        Assert.Equal(1, r.Progress.Done);
    }

    [Fact]
    public void Tasks_EligibleScholarshipBecomesMilestoneTask()
    {
        var sch = new RoadmapScholarshipRow(Guid.NewGuid(), SchoolA, "Merit", 3.5m, null, null, null, null, new DateOnly(2027, 12, 1));
        var past = new RoadmapScholarshipRow(Guid.NewGuid(), SchoolA, "Cũ", null, null, null, null, null, new DateOnly(2026, 1, 1));

        var r = Build(new StudentProfile { OverallGpa = 3.8m, TargetIntake = "Fall 2028" }, [], [Target(SchoolA, "A")],
            scholarships: [sch, past]);

        var school = Assert.Single(r.Schools);
        Assert.Single(school.Scholarships); // học bổng hết hạn bị bỏ
        var task = r.Tasks.Single(t => t.Id == $"scholarship:{sch.Id}");
        Assert.False(task.Checkable);
        Assert.Equal(["SAN_HOC_BONG"], task.Services);
        Assert.Contains(r.Milestones, m => m.Kind == "scholarship" && m.Date == new DateOnly(2027, 12, 1));
    }

    [Fact]
    public void ProvinceMissing_ReflectsProfile()
    {
        Assert.True(Build(new StudentProfile(), [], []).ProvinceMissing);
        Assert.False(Build(new StudentProfile { Province = "Hà Nội" }, [], []).ProvinceMissing);
    }
}
