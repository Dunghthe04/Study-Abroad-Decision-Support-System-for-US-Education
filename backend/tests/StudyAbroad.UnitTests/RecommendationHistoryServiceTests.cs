using System.Text.Json;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// UC-87: xem lịch sử gợi ý và so sánh hai lần chạy. Học sinh xem của mình; phụ huynh có liên kết đang hiệu lực xem của con.
public class RecommendationHistoryServiceTests
{
    private readonly FakeRecommendationHistoryRepository _history = new();
    private readonly FakeParentLinkRepository _links = new();

    private readonly Guid _student = Guid.NewGuid();
    private readonly Guid _otherStudent = Guid.NewGuid();
    private readonly Guid _parent = Guid.NewGuid();

    private RecommendationHistoryService CreateService() => new(_history, _links);

    private void LinkParent(string status) =>
        _links.Links.Add(new ParentStudentLink { StudentUserId = _student, ParentUserId = _parent, ParentEmail = "me@test.com", Status = status });

    // Thêm một lần chạy theo khuôn JSON RecommendationService đang lưu. Mỗi khối là danh sách mã trường theo thứ tự.
    private Recommendation AddRun(
        Guid userId, DateTime createdAt,
        string[]? reach = null, string[]? match = null, string[]? safety = null, string[]? aid = null,
        int[]? pref = null)
    {
        static object Block(string[]? codes) => (codes ?? []).Select(c => new { code = c, x = 0.5, why = Array.Empty<string>() }).ToList();
        var all = new[] { reach, match, safety, aid }.SelectMany(b => b ?? []).Distinct();
        var row = new Recommendation
        {
            UserId = userId,
            CreatedAt = createdAt,
            StudyLevel = "undergraduate",
            CriteriaJson = JsonSerializer.Serialize(new { major = "Computer Science", pref = pref ?? [2, 2, 2], tier = 1 }),
            ItemsJson = JsonSerializer.Serialize(new
            {
                scored = all.Select(c => new { code = c, name = "Trường " + c }).ToList(),
                failed = Array.Empty<string>(),
                reach = Block(reach),
                match = Block(match),
                safety = Block(safety),
                aid = Block(aid),
                other = Block(null),
            }),
        };
        _history.Rows.Add(row);
        return row;
    }

    private Recommendation AddLegacyRun(Guid userId, DateTime createdAt)
    {
        var row = new Recommendation { UserId = userId, CreatedAt = createdAt, ItemsJson = "[{\"universityId\":1}]" };
        _history.Rows.Add(row);
        return row;
    }

    private static readonly DateTime Day1 = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    // 1
    [Fact]
    public async Task List_Own_NewestFirstWithCountsAndPref()
    {
        AddRun(_student, Day1, reach: ["A"], match: ["B", "C"], safety: ["D"], pref: [4, 0, 2]);
        AddRun(_student, Day1.AddDays(1), match: ["B"]);
        AddRun(_otherStudent, Day1.AddDays(2), match: ["X"]);

        var runs = await CreateService().ListAsync(_student, null, CancellationToken.None);

        Assert.NotNull(runs);
        Assert.Equal(2, runs.Count);                                  // không lẫn của học sinh khác
        Assert.Equal(Day1.AddDays(1), runs[0].CreatedAt);              // mới nhất trước
        Assert.Equal(new RunGroupCounts(1, 2, 1, 0), runs[1].Counts);
        Assert.Equal([4, 0, 2], runs[1].Pref);
        Assert.Equal("Computer Science", runs[1].Major);
    }

    // 2
    [Fact]
    public async Task List_SkipsLegacyRows()
    {
        AddLegacyRun(_student, Day1);
        AddRun(_student, Day1.AddDays(1), match: ["B"]);

        var runs = await CreateService().ListAsync(_student, null, CancellationToken.None);

        Assert.Single(runs!);
    }

    // 3
    [Fact]
    public async Task List_AsksRepositoryForAtMost50()
    {
        await CreateService().ListAsync(_student, null, CancellationToken.None);

        Assert.Equal(RecommendationHistoryService.MaxRuns, _history.LastTake);
        Assert.Equal(50, RecommendationHistoryService.MaxRuns);
    }

    // 4
    [Fact]
    public async Task List_ActiveParent_SeesChildRuns()
    {
        LinkParent(ParentLinkStatuses.Active);
        AddRun(_student, Day1, match: ["B"]);

        var runs = await CreateService().ListAsync(_parent, _student, CancellationToken.None);

        Assert.Single(runs!);
    }

    // 5
    [Theory]
    [InlineData(ParentLinkStatuses.Pending)]
    [InlineData(ParentLinkStatuses.Revoked)]
    public async Task List_ParentWithoutActiveLink_ReturnsNull(string status)
    {
        LinkParent(status);
        AddRun(_student, Day1, match: ["B"]);

        Assert.Null(await CreateService().ListAsync(_parent, _student, CancellationToken.None));
    }

    // 6
    [Fact]
    public async Task List_OtherStudent_ReturnsNull()
    {
        AddRun(_student, Day1, match: ["B"]);

        Assert.Null(await CreateService().ListAsync(_otherStudent, _student, CancellationToken.None));
    }

    // 7
    [Fact]
    public async Task Get_Own_ReturnsRow()
    {
        var run = AddRun(_student, Day1, match: ["B"]);

        var found = await CreateService().GetAsync(_student, run.Id, CancellationToken.None);

        Assert.Equal(run.Id, found?.Id);
    }

    // 8
    [Fact]
    public async Task Get_OtherStudentOrLegacyOrMissing_ReturnsNull()
    {
        var run = AddRun(_student, Day1, match: ["B"]);
        var legacy = AddLegacyRun(_student, Day1);
        var service = CreateService();

        Assert.Null(await service.GetAsync(_otherStudent, run.Id, CancellationToken.None));
        Assert.Null(await service.GetAsync(_student, legacy.Id, CancellationToken.None));
        Assert.Null(await service.GetAsync(_student, Guid.NewGuid(), CancellationToken.None));
    }

    // 9
    [Fact]
    public async Task Get_ActiveParent_ReturnsChildRow()
    {
        LinkParent(ParentLinkStatuses.Active);
        var run = AddRun(_student, Day1, match: ["B"]);

        Assert.NotNull(await CreateService().GetAsync(_parent, run.Id, CancellationToken.None));
    }

    // 10
    [Fact]
    public async Task Compare_ReportsGroupRankNewAndDropped_SkipsUnchanged()
    {
        var from = AddRun(_student, Day1, reach: ["A"], match: ["B", "C"], safety: ["D", "E"]);
        var to = AddRun(_student, Day1.AddDays(1), reach: ["F"], match: ["A", "C", "B"], safety: ["D"]);

        var result = await CreateService().CompareAsync(_student, from.Id, to.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(from.Id, result.From.Id);
        Assert.Equal(to.Id, result.To.Id);
        var byCode = result.Changes.ToDictionary(c => c.Code);
        Assert.Equal(new RunSchoolChange("F", "Trường F", null, "reach", null, 1), byCode["F"]);         // mới vào
        Assert.Equal(new RunSchoolChange("A", "Trường A", "reach", "match", 1, 1), byCode["A"]);        // đổi nhóm
        Assert.Equal(new RunSchoolChange("B", "Trường B", "match", "match", 1, 3), byCode["B"]);        // đổi hạng
        Assert.Equal(new RunSchoolChange("E", "Trường E", "safety", null, 2, null), byCode["E"]);       // rơi ra
        Assert.DoesNotContain("C", byCode.Keys);                                                        // cùng nhóm, cùng hạng
        Assert.DoesNotContain("D", byCode.Keys);
        Assert.Equal(["F", "A", "B", "E"], result.Changes.Select(c => c.Code));                         // theo nhóm/hạng lần sau, rơi ra cuối
    }

    // 11
    [Fact]
    public async Task Compare_CodesMatchIgnoringCase()
    {
        var from = AddRun(_student, Day1, match: ["abc"]);
        var to = AddRun(_student, Day1.AddDays(1), match: ["ABC"]);

        var result = await CreateService().CompareAsync(_student, from.Id, to.Id, CancellationToken.None);

        Assert.Empty(result!.Changes);
    }

    // 12
    [Fact]
    public async Task Compare_RunsOfTwoStudents_ReturnsNull()
    {
        var mine = AddRun(_student, Day1, match: ["B"]);
        var theirs = AddRun(_otherStudent, Day1, match: ["B"]);

        Assert.Null(await CreateService().CompareAsync(_student, mine.Id, theirs.Id, CancellationToken.None));
    }

    // 13
    [Fact]
    public async Task Compare_NoPermissionOrLegacyOrMissing_ReturnsNull()
    {
        var a = AddRun(_student, Day1, match: ["B"]);
        var b = AddRun(_student, Day1.AddDays(1), match: ["C"]);
        var legacy = AddLegacyRun(_student, Day1);
        var service = CreateService();

        Assert.Null(await service.CompareAsync(_otherStudent, a.Id, b.Id, CancellationToken.None));
        Assert.Null(await service.CompareAsync(_student, a.Id, legacy.Id, CancellationToken.None));
        Assert.Null(await service.CompareAsync(_student, a.Id, Guid.NewGuid(), CancellationToken.None));
    }

    // 14
    [Fact]
    public async Task Compare_ActiveParent_CanCompareChildRuns()
    {
        LinkParent(ParentLinkStatuses.Active);
        var a = AddRun(_student, Day1, match: ["B"]);
        var b = AddRun(_student, Day1.AddDays(1), safety: ["B"]);

        var result = await CreateService().CompareAsync(_parent, a.Id, b.Id, CancellationToken.None);

        Assert.Equal("safety", Assert.Single(result!.Changes).ToGroup);
    }
}

/// <summary>Giả lập RecommendationHistoryRepository (UC-87): giữ các lần chạy trong bộ nhớ.</summary>
internal sealed class FakeRecommendationHistoryRepository : IRecommendationHistoryRepository
{
    public List<Recommendation> Rows { get; } = [];
    public int? LastTake { get; private set; }

    public Task<List<Recommendation>> ListByUserAsync(Guid userId, int take, CancellationToken ct = default)
    {
        LastTake = take;
        return Task.FromResult(Rows.Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).Take(take).ToList());
    }

    public Task<Recommendation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));
}
