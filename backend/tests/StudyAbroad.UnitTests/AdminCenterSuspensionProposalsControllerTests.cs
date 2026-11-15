using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[USAS-2080 / UC-58] Controller đề nghị tạm ngừng trung tâm: phân quyền và ánh xạ lỗi.</summary>
public class AdminCenterSuspensionProposalsControllerTests
{
    private sealed class FakeService : ICenterSuspensionProposalService
    {
        public Exception? ToThrow { get; set; }
        public (Guid UserId, Guid CenterId, string? Reason)? LastPropose { get; private set; }
        public (Guid UserId, Guid Id, bool Approve, string? Note)? LastDecide { get; private set; }

        private static CenterSuspensionProposalDto Dto(Guid centerId, string status) =>
            new(Guid.NewGuid(), centerId, "Trung tâm", Guid.NewGuid(), "Lý do", status, 3, DateTime.UtcNow, null, null, null);

        public Task<IReadOnlyList<RepeatRemovalCenterDto>> GetRepeatRemovalCentersAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepeatRemovalCenterDto>>([new RepeatRemovalCenterDto(Guid.NewGuid(), "Trung tâm", "verified", 4, false)]);

        public Task<CenterSuspensionProposalDto> ProposeAsync(Guid moderatorUserId, Guid centerId, string? reason, CancellationToken ct = default)
        {
            if (ToThrow is not null) throw ToThrow;
            LastPropose = (moderatorUserId, centerId, reason);
            return Task.FromResult(Dto(centerId, "pending"));
        }

        public Task<PagedResult<CenterSuspensionProposalDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
        {
            if (ToThrow is not null) throw ToThrow;
            return Task.FromResult(new PagedResult<CenterSuspensionProposalDto>([], page, pageSize, 0));
        }

        public Task<CenterSuspensionProposalDto> DecideAsync(Guid adminUserId, Guid proposalId, bool approve, string? note, CancellationToken ct = default)
        {
            if (ToThrow is not null) throw ToThrow;
            LastDecide = (adminUserId, proposalId, approve, note);
            return Task.FromResult(Dto(Guid.NewGuid(), approve ? "approved" : "rejected"));
        }
    }

    private static (AdminCenterSuspensionProposalsController Controller, FakeService Service) Create(Guid userId, string role)
    {
        var service = new FakeService();
        var controller = new AdminCenterSuspensionProposalsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, role),
                    ], "TestAuth"))
                }
            }
        };
        return (controller, service);
    }

    // ValidationProblem trả ObjectResult chứa ProblemDetails (mã 400 do pipeline ASP.NET gắn khi chạy thật), cùng cách các test controller khác kiểm tra
    private static void AssertValidationProblem(IActionResult result) =>
        Assert.IsAssignableFrom<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);

    private static string? RolesOf(string method) =>
        typeof(AdminCenterSuspensionProposalsController).GetMethod(method)
            ?.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Roles;

    [Fact]
    public void Actions_HaveExpectedRoleRestrictions()
    {
        Assert.Equal($"{UserRoles.Moderator},{UserRoles.Admin}", RolesOf(nameof(AdminCenterSuspensionProposalsController.GetRepeatRemovalCenters)));
        Assert.Equal($"{UserRoles.Moderator},{UserRoles.Admin}", RolesOf(nameof(AdminCenterSuspensionProposalsController.List)));
        Assert.Equal(UserRoles.Moderator, RolesOf(nameof(AdminCenterSuspensionProposalsController.Propose)));
        Assert.Equal(UserRoles.Admin, RolesOf(nameof(AdminCenterSuspensionProposalsController.Decide)));
    }

    [Fact]
    public async Task Propose_AsModerator_Returns201AndPassesArguments()
    {
        var moderatorId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var (controller, service) = Create(moderatorId, UserRoles.Moderator);

        var result = await controller.Propose(new CreateSuspensionProposalRequest(centerId, "Gỡ nhiều lần"), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, obj.StatusCode);
        Assert.Equal((moderatorId, centerId, "Gỡ nhiều lần"), service.LastPropose);
    }

    [Fact]
    public async Task Propose_RuleViolation_ReturnsValidationProblemAndMissingCenter_Returns404()
    {
        var (controller, service) = Create(Guid.NewGuid(), UserRoles.Moderator);
        var request = new CreateSuspensionProposalRequest(Guid.NewGuid(), "x");

        service.ToThrow = new ArgumentException("Chưa đủ số tin bị gỡ.");
        AssertValidationProblem(await controller.Propose(request, CancellationToken.None));

        service.ToThrow = new KeyNotFoundException("Không tìm thấy trung tâm.");
        Assert.IsType<NotFoundObjectResult>(await controller.Propose(request, CancellationToken.None));
    }

    [Fact]
    public async Task Decide_AsAdmin_ReturnsOkAndPassesArguments()
    {
        var adminId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var (controller, service) = Create(adminId, UserRoles.Admin);

        var result = await controller.Decide(proposalId, new DecideSuspensionProposalRequest(true, "Đồng ý"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("approved", Assert.IsType<CenterSuspensionProposalDto>(ok.Value).Status);
        Assert.Equal((adminId, proposalId, true, "Đồng ý"), service.LastDecide);
    }

    [Fact]
    public async Task Decide_AlreadyDecided_ReturnsValidationProblem()
    {
        var (controller, service) = Create(Guid.NewGuid(), UserRoles.Admin);
        service.ToThrow = new ArgumentException("Đề nghị này đã được quyết định.");

        var result = await controller.Decide(Guid.NewGuid(), new DecideSuspensionProposalRequest(false), CancellationToken.None);

        AssertValidationProblem(result);
    }

    [Fact]
    public async Task List_ReturnsOkPage()
    {
        var (controller, _) = Create(Guid.NewGuid(), UserRoles.Moderator);

        var result = await controller.List("pending", 1, 20, CancellationToken.None);

        Assert.IsType<PagedResult<CenterSuspensionProposalDto>>(Assert.IsType<OkObjectResult>(result).Value);
    }
}
