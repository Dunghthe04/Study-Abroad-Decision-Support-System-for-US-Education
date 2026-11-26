using StudyAbroad.Application.Dashboard.Roadmap;

namespace StudyAbroad.UnitTests;

/// <summary>Roadmap gửi sang advisor: khóa camelCase như các field khác, bỏ danh sách trung tâm (chat không dùng).</summary>
public class RoadmapForAdvisorTests
{
    private static RoadmapDto Sample() => new(
        Mode: "target",
        Intake: "Fall 2027",
        IntakeIsEstimate: false,
        Schools:
        [
            new RoadmapSchoolDto(Guid.NewGuid(), "Trường A", "match",
                [new RoadmapCriterionDto("gpa", null, 3.6m, 3.8m, "near")], 0, 1, []),
        ],
        Summary: "Đạt đủ điều kiện 0/1 trường mục tiêu",
        Milestones: [],
        Tasks:
        [
            new RoadmapTaskDto("english_test", "Thi IELTS", new DateOnly(2027, 3, 1), "todo", true, [], ["ielts"],
                [new RoadmapCenterDto(Guid.NewGuid(), "Trung tâm X", "Hà Nội", 4.5m, 10, true, null, true)]),
        ],
        Progress: new RoadmapProgressDto(0, 1),
        ProvinceMissing: true);

    [Fact]
    public void Slim_Null_ReturnsNull() => Assert.Null(RoadmapForAdvisor.Slim(null));

    [Fact]
    public void Slim_UsesCamelCaseAndDropsCenters()
    {
        var json = RoadmapForAdvisor.Slim(Sample())!.Value;

        Assert.Equal("target", json.GetProperty("mode").GetString());
        Assert.True(json.GetProperty("provinceMissing").GetBoolean());
        Assert.Equal("near", json.GetProperty("schools")[0].GetProperty("criteria")[0].GetProperty("status").GetString());
        var task = json.GetProperty("tasks")[0];
        Assert.Equal("2027-03-01", task.GetProperty("due").GetString());
        Assert.False(task.TryGetProperty("centers", out _));
    }
}
