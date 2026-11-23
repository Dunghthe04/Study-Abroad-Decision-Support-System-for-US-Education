using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Dashboard;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

public class DashboardControllerTests
{
    private readonly FakeDashboardService _service = new();

    private DashboardController CreateController(Guid? userId = null, string role = UserRoles.Student)
    {
        var controller = new DashboardController(_service);
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                new Claim(ClaimTypes.Role, role)
            ], "TestAuth"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task GetMyDashboard_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var controller = CreateController(userId: null);
        var result = await controller.GetMyDashboard(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetMyDashboard_WhenAuthenticated_ReturnsOkWithDashboard()
    {
        var userId = Guid.NewGuid();
        var controller = CreateController(userId: userId, role: UserRoles.Student);

        var result = await controller.GetMyDashboard(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<StudentDashboardDto>(okResult.Value);
        Assert.NotNull(dto);
        Assert.Equal(userId, _service.LastRequestedStudentId);
    }

    [Fact]
    public async Task GetChildDashboard_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var controller = CreateController(userId: null);
        var result = await controller.GetChildDashboard(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetChildDashboard_WhenChildNotFoundOrInactiveLink_ReturnsNotFound()
    {
        var parentId = Guid.NewGuid();
        var controller = CreateController(userId: parentId, role: UserRoles.Parent);
        _service.ParentChildResult = null;

        var result = await controller.GetChildDashboard(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetChildDashboard_WhenValidParentLink_ReturnsOkWithChildDashboard()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var controller = CreateController(userId: parentId, role: UserRoles.Parent);

        var mockChildDto = new ParentChildDashboardDto(
            childId,
            "Nguyễn Văn Con",
            false,
            CreateMockStudentDashboard(),
            CreateMockParentFinancialKpi()
        );
        _service.ParentChildResult = mockChildDto;

        var result = await controller.GetChildDashboard(childId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ParentChildDashboardDto>(okResult.Value);
        Assert.Equal(childId, dto.StudentId);
        Assert.Equal("Nguyễn Văn Con", dto.StudentName);
        Assert.Equal(parentId, _service.LastRequestedParentId);
        Assert.Equal(childId, _service.LastRequestedChildId);
    }

    private static StudentDashboardDto CreateMockStudentDashboard() =>
        new(
            new RecommendationKpiDto(1, 2, 1, 0, 4, DateTime.UtcNow, "Giải thích", true),
            new ProfileProgressKpiDto(3, 5, false, "3/5 bước"),
            new UpcomingDeadlinesKpiDto([], 0, 0, "Không có"),
            new DataCompletenessKpiDto(5, 5, [], [], "Đầy đủ")
        );

    private static ParentFinancialKpiDto CreateMockParentFinancialKpi() =>
        new(
            BudgetPerYear: 30000,
            BudgetSource: "parent_preference",
            BudgetSourceLabel: "Theo ngân sách",
            SchoolsWithinBudgetCount: 2,
            SchoolsTotalWithCostCount: 2,
            SchoolsMissingCostCount: 0,
            TotalCost4YearsAllSchools: 240000,
            ShortfallList: [],
            MostShortfallSchool: null,
            LeastShortfallSchool: null,
            WithinBudgetExplanation: "Giải thích",
            TotalCost4YearsExplanation: "Giải thích",
            ShortfallExplanation: "Giải thích"
        );

    private sealed class FakeDashboardService : IDashboardService
    {
        public Guid? LastRequestedStudentId { get; private set; }
        public Guid? LastRequestedParentId { get; private set; }
        public Guid? LastRequestedChildId { get; private set; }
        public ParentChildDashboardDto? ParentChildResult { get; set; }

        public Task<StudentDashboardDto> GetStudentDashboardAsync(Guid studentId, CancellationToken ct = default)
        {
            LastRequestedStudentId = studentId;
            return Task.FromResult(CreateMockStudentDashboard());
        }

        public Task<ParentChildDashboardDto?> GetParentChildDashboardAsync(Guid parentId, Guid studentId, CancellationToken ct = default)
        {
            LastRequestedParentId = parentId;
            LastRequestedChildId = studentId;
            return Task.FromResult(ParentChildResult);
        }
    }
}
