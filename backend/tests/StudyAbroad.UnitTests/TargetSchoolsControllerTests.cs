using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using System.Security.Claims;
using Xunit;

namespace StudyAbroad.UnitTests;

public class TargetSchoolsControllerTests
{
    private sealed class FakeTargetSchoolService : ITargetSchoolService
    {
        public Task<AddTargetSchoolResult> AddTargetSchoolAsync(Guid userId, AddTargetSchoolRequest request, CancellationToken ct = default)
        {
            var dto = new TargetSchoolDto(
                Guid.NewGuid(),
                userId,
                Guid.NewGuid(),
                "110653",
                "UC Irvine",
                "Irvine",
                "CA",
                "https://uci.edu",
                "public",
                0.21m,
                "reach",
                "considering",
                0,
                null,
                null,
                null,
                DateTime.UtcNow,
                null
            );
            return Task.FromResult(AddTargetSchoolResult.Ok(dto));
        }

        public Task<TargetSchoolDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) =>
            Task.FromResult<TargetSchoolDto?>(null);

        public Task<bool> RemoveTargetSchoolAsync(Guid userId, Guid targetSchoolId, CancellationToken ct = default) =>
            Task.FromResult(targetSchoolId != Guid.Empty);

        public Task<bool> RemoveTargetSchoolByUniversityAsync(Guid userId, string universityIdentifier, CancellationToken ct = default) =>
            Task.FromResult(!string.IsNullOrWhiteSpace(universityIdentifier));

        public Task<TargetSchoolDto?> CheckTargetSchoolAsync(Guid userId, string? universityCode, Guid? universityId, CancellationToken ct = default) =>
            Task.FromResult<TargetSchoolDto?>(null);

        public Task<TargetSchoolSummaryDto> GetTargetSchoolsAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default) =>
            Task.FromResult(new TargetSchoolSummaryDto([], 0, 0, 0, 0, 0, null));

        public Task<UpdateApplicationRoundResult> UpdateApplicationRoundAsync(Guid userId, Guid targetSchoolId, UpdateApplicationRoundRequest request, CancellationToken ct = default)
        {
            if (targetSchoolId == Guid.Empty)
                return Task.FromResult(UpdateApplicationRoundResult.NotFoundError("Không tìm thấy trường mục tiêu trong danh sách của bạn."));

            var dto = new TargetSchoolDto(
                targetSchoolId,
                userId,
                Guid.NewGuid(),
                "110653",
                "UC Irvine",
                "Irvine",
                "CA",
                "https://uci.edu",
                "public",
                0.21m,
                "reach",
                "considering",
                0,
                null,
                request.ApplicationRound,
                request.Deadline,
                DateTime.UtcNow,
                DateTime.UtcNow
            );
            return Task.FromResult(UpdateApplicationRoundResult.Ok(dto));
        }

        public Task<UpdateTargetCategoryResult> UpdateCategoryAsync(Guid userId, Guid targetSchoolId, UpdateTargetCategoryRequest request, CancellationToken ct = default)
        {
            if (targetSchoolId == Guid.Empty)
                return Task.FromResult(UpdateTargetCategoryResult.NotFoundError("Không tìm thấy trường mục tiêu trong danh sách của bạn."));

            var dto = new TargetSchoolDto(
                targetSchoolId, userId, Guid.NewGuid(), "110653", "UC Irvine", "Irvine", "CA", "https://uci.edu",
                "public", 0.21m, request.Category, "considering", 0, null, null, null, DateTime.UtcNow, DateTime.UtcNow,
                IsCategoryUserSelected: true);
            return Task.FromResult(UpdateTargetCategoryResult.Ok(dto));
        }
    }

    [Fact]
    public async Task AddTargetSchool_ReturnsCreatedAtAction()
    {
        var service = new FakeTargetSchoolService();
        var controller = new TargetSchoolsController(service);

        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "student")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var actionResult = await controller.AddTargetSchool(new AddTargetSchoolRequest(null, "110653", "reach", null, null, null), default);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        Assert.Equal(nameof(TargetSchoolsController.GetById), createdResult.ActionName);
    }

    [Fact]
    public async Task RemoveTargetSchool_WhenSuccessful_ReturnsNoContent()
    {
        var service = new FakeTargetSchoolService();
        var controller = new TargetSchoolsController(service);

        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "student")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var result = await controller.RemoveTargetSchool(Guid.NewGuid(), default);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveByUniversity_WhenSuccessful_ReturnsNoContent()
    {
        var service = new FakeTargetSchoolService();
        var controller = new TargetSchoolsController(service);

        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "student")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var result = await controller.RemoveByUniversity("110653", default);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetTargetSchools_ReturnsOkWithSummary()
    {
        var service = new FakeTargetSchoolService();
        var controller = new TargetSchoolsController(service);

        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "student")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var result = await controller.GetTargetSchools(null, null, default);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<TargetSchoolSummaryDto>(okResult.Value);
        Assert.Equal(0, summary.TotalCount);
    }

    [Fact]
    public async Task UpdateApplicationRound_WhenNotFound_Returns404()
    {
        var controller = new TargetSchoolsController(new FakeTargetSchoolService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Role, "student")
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.UpdateApplicationRound(Guid.Empty, new UpdateApplicationRoundRequest("Regular Decision"), default);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateCategory_WhenValid_ReturnsOkWithUserSelectedFlag()
    {
        var controller = new TargetSchoolsController(new FakeTargetSchoolService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Role, "student")
                    ], "TestAuth"))
                }
            }
        };

        var result = await controller.UpdateCategory(Guid.NewGuid(), new UpdateTargetCategoryRequest("safety"), default);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TargetSchoolDto>(ok.Value);
        Assert.Equal("safety", dto.Category);
        Assert.True(dto.IsCategoryUserSelected);
    }

    [Fact]
    public void Controller_RestrictsAllActionsToStudentAndParent()
    {
        var type = typeof(TargetSchoolsController);
        var authorize = type.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal($"{UserRoles.Student},{UserRoles.Parent}", authorize!.Roles);
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.All(
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            m => Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>()));
    }
}
