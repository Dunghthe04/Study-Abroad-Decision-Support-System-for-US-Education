using System;
using System.Collections.Generic;
using System.Linq;
using StudyAbroad.Application.Dashboard.Roadmap;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>Gợi ý trung tâm theo nhiệm vụ (kế hoạch mục 6.4).</summary>
public class RoadmapCentersTests
{
    private static readonly DateOnly Today = new(2026, 11, 26);
    private static readonly Guid Student = Guid.NewGuid();

    private static StudyCenter Center(string name, string city = "Hà Nội", decimal rating = 5m, int reviews = 0,
        string service = "LUYEN_THI_IELTS", bool legalHold = false, List<string>? levels = null) =>
        new()
        {
            Id = Guid.NewGuid(), Name = name, City = city, AverageRating = rating, ReviewCount = reviews,
            Services = [service], IsActive = true, IsLegalHold = legalHold, VerificationStatus = "verified",
            StudyLevels = levels ?? [],
        };

    private static RoadmapTaskDto Task(string id, string status = "todo", params string[] services) =>
        new(id, id, Today, status, true, [], services, []);

    private static RoadmapDto Roadmap(params RoadmapTaskDto[] tasks) =>
        new("target", "Fall 2028", false, [], "", [], tasks, new RoadmapProgressDto(0, 0), false);

    [Fact]
    public void SameProvinceFirst_ThenRealRating_UnratedCountsAsZero()
    {
        var pool = new List<StudyCenter>
        {
            Center("Chưa đánh giá HN"),                         // mặc định 5.0 nhưng 0 đánh giá -> 0
            Center("HN 4.2", rating: 4.2m, reviews: 10),
            Center("HCM 5.0", city: "TP. Hồ Chí Minh", rating: 5m, reviews: 30),
        };

        var picked = RoadmapCenters.Suggest(pool, ["LUYEN_THI_IELTS"], "Hà Nội", null, Student, Today);

        Assert.Equal(["HN 4.2", "Chưa đánh giá HN", "HCM 5.0"], picked.Select(c => c.Name));
        Assert.Equal(0m, picked[1].Rating);
        Assert.True(picked[0].SameProvince);
        Assert.False(picked[2].SameProvince); // bù toàn quốc, giao diện hiện câu báo
    }

    [Fact]
    public void NoProvince_RanksNationwideByRating_TakesThree()
    {
        var pool = Enumerable.Range(1, 5).Select(i => Center($"C{i}", rating: i, reviews: 1)).ToList();

        var picked = RoadmapCenters.Suggest(pool, ["LUYEN_THI_IELTS"], null, null, Student, Today);

        Assert.Equal(["C5", "C4", "C3"], picked.Select(c => c.Name));
        Assert.All(picked, c => Assert.False(c.SameProvince));
    }

    [Fact]
    public void Ties_AreStableForSameStudentAndDay()
    {
        var pool = Enumerable.Range(1, 8).Select(i => Center($"C{i}")).ToList();

        var first = RoadmapCenters.Suggest(pool, ["LUYEN_THI_IELTS"], null, null, Student, Today).Select(c => c.Id);
        var again = RoadmapCenters.Suggest(pool.AsEnumerable().Reverse().ToList(), ["LUYEN_THI_IELTS"], null, null, Student, Today).Select(c => c.Id);

        Assert.Equal(first, again);
    }

    [Fact]
    public void FiltersByServiceAndLevel_EmptyLevelsAcceptsAll()
    {
        var pool = new List<StudyCenter>
        {
            Center("SAT", service: "LUYEN_THI_SAT"),
            Center("Thạc sĩ", levels: ["master"]),
            Center("Mọi bậc"),
            Center("Đại học", levels: ["undergraduate"]),
        };

        var picked = RoadmapCenters.Suggest(pool, ["LUYEN_THI_IELTS"], null, "undergraduate", Student, Today);

        Assert.Equal(["Mọi bậc", "Đại học"], picked.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Attach_SkipsDoneTasksAndLegalHold()
    {
        var pool = new List<StudyCenter> { Center("Ổn"), Center("Bị giữ", legalHold: true) };
        var roadmap = Roadmap(
            Task("english:ielts", "todo", "LUYEN_THI_IELTS"),
            Task("english:toefl", "done", "LUYEN_THI_IELTS"),
            Task("apply:x"));

        var r = RoadmapCenters.Attach(roadmap, pool, null, null, Student, Today);

        Assert.Equal(["Ổn"], r.Tasks[0].Centers.Select(c => c.Name));
        Assert.Empty(r.Tasks[1].Centers);
        Assert.Empty(r.Tasks[2].Centers);
    }

    [Fact]
    public void FeeText_PrefersDescription_ElseFormatsRange()
    {
        Assert.Equal("Theo khóa", RoadmapCenters.FeeText(new StudyCenter { FeeDescription = " Theo khóa " }));
        Assert.Equal("1.000.000 – 5.000.000 đ", RoadmapCenters.FeeText(new StudyCenter { FeeMin = 1_000_000, FeeMax = 5_000_000 }));
        Assert.Equal("từ 2.000.000 đ", RoadmapCenters.FeeText(new StudyCenter { FeeMin = 2_000_000 }));
        Assert.Null(RoadmapCenters.FeeText(new StudyCenter()));
    }
}
