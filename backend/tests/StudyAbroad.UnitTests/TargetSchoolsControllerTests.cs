using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.TargetSchools;
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
}
