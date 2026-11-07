using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

// UC-87: controller trả đúng mã HTTP. Quyền chi tiết (ai xem được) đã test ở RecommendationHistoryServiceTests.
public class RecommendationHistoryControllerTests
{
    private readonly FakeRecommendationHistoryRepository _history = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly Guid _student = Guid.NewGuid();
    private readonly Guid _otherStudent = Guid.NewGuid();

    private RecommendationHistoryController CreateController(Guid userId) =>
        new(new RecommendationHistoryService(_history, _links))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, UserRoles.Student),
                    ], "TestAuth"))
                }
            }
        };

    private Recommendation AddRun(Guid userId, string matchCode, int day = 1)
    {
        var row = new Recommendation
        {
            UserId = userId,
            CreatedAt = new DateTime(2026, 11, day, 8, 0, 0, DateTimeKind.Utc),
            StudyLevel = "undergraduate",
            AlgorithmVersion = "test",
            CriteriaJson = JsonSerializer.Serialize(new { major = "Biology", pref = new[] { 2, 2, 2 } }),
            ItemsJson = JsonSerializer.Serialize(new
            {
                scored = new[] { new { code = matchCode, name = "Trường " + matchCode } },
                reach = Array.Empty<object>(),
                match = new[] { new { code = matchCode, x = 0.5 } },
                safety = Array.Empty<object>(),
                aid = Array.Empty<object>(),
            }),
        };
        _history.Rows.Add(row);
        return row;
    }

    [Fact]
    public async Task List_Own_Returns200()
    {
        AddRun(_student, "A");

        var result = await CreateController(_student).List(null, CancellationToken.None);

        var runs = Assert.IsAssignableFrom<IReadOnlyList<RecommendationRunSummaryDto>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(runs);
    }

    [Fact]
    public async Task List_OtherStudent_Returns404()
    {
        var result = await CreateController(_otherStudent).List(_student, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Get_Own_ReturnsSameShapeAsLatest()
    {
        var run = AddRun(_student, "A");

        var result = await CreateController(_student).Get(run.Id, CancellationToken.None);

        var body = Assert.IsType<RecommendationsController.RecommendationResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(run.Id, body.Id);
        Assert.Equal("test", body.AlgorithmVersion);
        Assert.Equal("A", body.Items.GetProperty("match")[0].GetProperty("code").GetString());
        Assert.Equal("Biology", body.Criteria.GetProperty("major").GetString());
    }

    [Fact]
    public async Task Get_OtherStudentsRun_Returns404()
    {
        var run = AddRun(_student, "A");

        var result = await CreateController(_otherStudent).Get(run.Id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Compare_TwoOwnRuns_Returns200WithChanges()
    {
        var from = AddRun(_student, "A", day: 1);
        var to = AddRun(_student, "B", day: 2);

        var result = await CreateController(_student).Compare(from.Id, to.Id, CancellationToken.None);

        var body = Assert.IsType<RunComparisonDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(["B", "A"], body.Changes.Select(c => c.Code));   // B mới vào, A rơi ra
    }

    [Fact]
    public async Task Compare_RunsOfTwoStudents_Returns404()
    {
        var mine = AddRun(_student, "A");
        var theirs = AddRun(_otherStudent, "B");

        var result = await CreateController(_student).Compare(mine.Id, theirs.Id, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Compare_SameOrEmptyId_Returns400()
    {
        var run = AddRun(_student, "A");
        var controller = CreateController(_student);

        Assert.IsType<BadRequestObjectResult>(await controller.Compare(run.Id, run.Id, CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(await controller.Compare(Guid.Empty, run.Id, CancellationToken.None));
    }

    [Fact]
    public async Task NoUserId_Returns401()
    {
        var controller = new RecommendationHistoryController(new RecommendationHistoryService(_history, _links))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.IsType<UnauthorizedObjectResult>(await controller.List(null, CancellationToken.None));
    }
}
