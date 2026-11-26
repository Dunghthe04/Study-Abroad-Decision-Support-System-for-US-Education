using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Dashboard.Roadmap;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>GET api/v1/dashboard/roadmap và children/{id}/roadmap: mã trả về.</summary>
public class RoadmapControllerTests
{
    private static readonly RoadmapDto Sample =
        new("average", "Fall 2027", true, [], "", [], [], new RoadmapProgressDto(0, 0), true);

    private static RoadmapController Create(IRoadmapService service, Guid? userId)
    {
        var http = new DefaultHttpContext();
        if (userId is { } id)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "TestAuth"));
        return new RoadmapController(service) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    [Fact]
    public async Task GetRoadmap_Unauthenticated_Returns401()
    {
        var result = await Create(new Stub(), null).GetRoadmap(CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetRoadmap_NoProfile_Returns204()
    {
        var result = await Create(new Stub(), Guid.NewGuid()).GetRoadmap(CancellationToken.None);
        Assert.IsType<NoContentResult>(result.Result);
    }

    [Fact]
    public async Task GetRoadmap_ReturnsRoadmapOfCaller()
    {
        var userId = Guid.NewGuid();
        var stub = new Stub { Roadmap = Sample };

        var result = await Create(stub, userId).GetRoadmap(CancellationToken.None);

        Assert.Same(Sample, Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(userId, stub.LastStudent);
    }

    [Fact]
    public async Task GetChildRoadmap_NotLinked_Returns404()
    {
        var result = await Create(new Stub { Linked = false }, Guid.NewGuid()).GetChildRoadmap(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetChildRoadmap_Linked_ReturnsOkOr204()
    {
        var ok = await Create(new Stub { Linked = true, Roadmap = Sample }, Guid.NewGuid()).GetChildRoadmap(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<OkObjectResult>(ok.Result);

        var empty = await Create(new Stub { Linked = true }, Guid.NewGuid()).GetChildRoadmap(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NoContentResult>(empty.Result);
    }

    private sealed class Stub : IRoadmapService
    {
        public RoadmapDto? Roadmap { get; init; }
        public bool Linked { get; init; }
        public Guid? LastStudent { get; private set; }

        public Task<RoadmapDto?> GetRoadmapAsync(Guid studentId, CancellationToken ct = default)
        {
            LastStudent = studentId;
            return Task.FromResult(Roadmap);
        }

        public Task<(bool Linked, RoadmapDto? Roadmap)> GetChildRoadmapAsync(Guid parentId, Guid studentId, CancellationToken ct = default) =>
            Task.FromResult((Linked, Linked ? Roadmap : null));

        public Task<SetProvinceResult> SetProvinceAsync(Guid studentId, string? province, CancellationToken ct = default) =>
            Task.FromResult(SetProvinceResult.Saved);
    }
}
