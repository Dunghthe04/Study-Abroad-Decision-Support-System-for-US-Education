using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.BirthDateChanges;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-150] Phân quyền màn Administrator duyệt yêu cầu sửa ngày sinh.</summary>
public class AdminBirthDateRequestsControllerTests
{
    private sealed class FakeService : IAdminBirthDateChangeService
    {
        public int EvidenceCalls;

        public Task<PagedResult<AdminBirthDateRequestDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<BirthDateChangeResult<AdminBirthDateRequestDto>> GetAsync(Guid requestId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<BirthDateChangeResult<BirthDateEvidenceFile>> OpenEvidenceAsync(Guid requestId, Guid adminId, AuditRequestInfo info, CancellationToken ct = default)
        {
            EvidenceCalls++;
            return Task.FromResult(BirthDateChangeResult<BirthDateEvidenceFile>.Ok(new BirthDateEvidenceFile(new MemoryStream([1, 2, 3]), "image/jpeg", "a.jpg"), ""));
        }
        public Task<BirthDateChangeResult<AdminBirthDateRequestDto>> ApproveAsync(Guid requestId, Guid adminId, AuditRequestInfo info, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<BirthDateChangeResult<AdminBirthDateRequestDto>> RejectAsync(Guid requestId, Guid adminId, string? rejectReason, AuditRequestInfo info, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static AdminBirthDateRequestsController Controller(FakeService svc, string role)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
        ], "test"));
        return new AdminBirthDateRequestsController(svc)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } },
        };
    }

    [Theory]
    [InlineData(UserRoles.Moderator)]
    [InlineData(UserRoles.Student)]
    public async Task GetEvidence_NotAdmin_ReportsNotFound_AUTH270(string role)
    {
        var svc = new FakeService();

        var result = await Controller(svc, role).GetEvidence(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Equal(0, svc.EvidenceCalls);   // không mở tệp, không ghi nhật ký xem
    }

    [Fact]
    public async Task GetEvidence_Admin_ReturnsFile()
    {
        var svc = new FakeService();

        var result = await Controller(svc, UserRoles.Admin).GetEvidence(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<FileStreamResult>(result);
        Assert.Equal(1, svc.EvidenceCalls);
    }

    [Theory]
    [InlineData(nameof(AdminBirthDateRequestsController.List))]
    [InlineData(nameof(AdminBirthDateRequestsController.Get))]
    [InlineData(nameof(AdminBirthDateRequestsController.Approve))]
    [InlineData(nameof(AdminBirthDateRequestsController.Reject))]
    public void OtherActions_AdminOnly(string action)
    {
        var attr = typeof(AdminBirthDateRequestsController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attr);
        Assert.Equal(UserRoles.Admin, attr.Roles);
    }
}
