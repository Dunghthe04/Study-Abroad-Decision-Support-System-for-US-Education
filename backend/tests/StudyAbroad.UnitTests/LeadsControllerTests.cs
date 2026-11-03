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

public class LeadsControllerTests
{
    private sealed class FakeLeadService : ILeadService
    {
        public List<LeadDto> Leads { get; } = [];

        public Task<LeadDto> CreateLeadAsync(Guid userId, CreateLeadRequest request, string? clientIp = null, CancellationToken ct = default)
        {
            if (request.StudyCenterId == Guid.Empty)
                throw new ArgumentException("Mã trung tâm tư vấn không được để trống.");

            var lead = new LeadDto(
                Guid.NewGuid(),
                userId,
                request.StudyCenterId,
                "Trung Tâm Du Học Test",
                request.SharedProfile ? Guid.NewGuid() : null,
                request.StudyLevel,
                request.ContactPhone,
                request.Message,
                request.SharedProfile,
                "new",
                DateTime.UtcNow);

            Leads.Add(lead);
            return Task.FromResult(lead);
        }

        public Task<LeadDto?> GetLeadByIdAsync(Guid userId, Guid leadId, CancellationToken ct = default)
        {
            var lead = Leads.FirstOrDefault(l => l.Id == leadId && l.UserId == userId);
            return Task.FromResult(lead);
        }

        public Task<PagedResult<LeadDto>> GetMyLeadsAsync(Guid userId, LeadQuery query, CancellationToken ct = default)
        {
            var list = Leads.Where(l => l.UserId == userId);
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                list = list.Where(l => string.Equals(l.Status, query.Status, StringComparison.OrdinalIgnoreCase));
            }

            var total = list.Count();
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var items = list.OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new PagedResult<LeadDto>(items, page, pageSize, total));
        }
    }

    private sealed class FakeAuthService(bool allowShare) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(allowShare ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(allowShare ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    private static (LeadsController Controller, FakeLeadService Service) CreateController(
        Guid userId,
        bool isMinor = false)
    {
        var fakeLeadService = new FakeLeadService();
        var fakeAuthService = new FakeAuthService(!isMinor);
        var controller = new LeadsController(fakeLeadService, fakeAuthService)
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

        return (controller, fakeLeadService);
    }

    [Fact]
    public async Task CreateLead_WithoutSharedProfile_Success_ReturnsCreatedAt()
    {
        var userId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var (controller, service) = CreateController(userId, isMinor: true);

        var request = new CreateLeadRequest(centerId, "bachelor", "0912345678", "Em muốn tư vấn du học Mỹ", false);
        var result = await controller.CreateLead(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<LeadDto>(created.Value);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal(centerId, dto.StudyCenterId);
        Assert.False(dto.SharedProfile);
        Assert.Null(dto.ConsentId);
    }

    [Fact]
    public async Task CreateLead_MinorSharesProfile_ReturnsForbiddenWithAgeRestrictedCode()
    {
        var userId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var (controller, _) = CreateController(userId, isMinor: true); // Chưa đủ 18 tuổi

        var request = new CreateLeadRequest(centerId, "bachelor", "0912345678", "Em gửi hồ sơ", true);
        var result = await controller.CreateLead(request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateLead_AdultSharesProfile_Success_CreatesConsent()
    {
        var userId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var (controller, service) = CreateController(userId, isMinor: false); // Đủ 18 tuổi

        var request = new CreateLeadRequest(centerId, "master", "0912345678", "Em muốn chia sẻ hồ sơ học thuật", true);
        var result = await controller.CreateLead(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<LeadDto>(created.Value);
        Assert.True(dto.SharedProfile);
        Assert.NotNull(dto.ConsentId);
    }

    [Fact]
    public async Task GetMyLeads_ReturnsPagedResult_FiltersByUserAndStatus()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var (controller, service) = CreateController(userId);

        // Seed leads cho user hiện tại: 2 lead "new", 1 lead "contacted"
        service.Leads.Add(new LeadDto(Guid.NewGuid(), userId, Guid.NewGuid(), "Center A", null, "bachelor", "0912345678", "Msg 1", false, "new", DateTime.UtcNow.AddMinutes(-10)));
        service.Leads.Add(new LeadDto(Guid.NewGuid(), userId, Guid.NewGuid(), "Center B", null, "master", "0912345678", "Msg 2", false, "new", DateTime.UtcNow.AddMinutes(-5)));
        service.Leads.Add(new LeadDto(Guid.NewGuid(), userId, Guid.NewGuid(), "Center C", null, "bachelor", "0912345678", "Msg 3", false, "contacted", DateTime.UtcNow));

        // Seed lead cho user khác (không được xuất hiện trong kết quả)
        service.Leads.Add(new LeadDto(Guid.NewGuid(), otherUserId, Guid.NewGuid(), "Center D", null, "bachelor", "0999999999", "Other", false, "new", DateTime.UtcNow));

        // Act: Lọc theo status "new"
        var actionResult = await controller.GetMyLeads(new LeadQuery(Status: "new", Page: 1, PageSize: 10), CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var paged = Assert.IsType<PagedResult<LeadDto>>(okResult.Value);

        Assert.Equal(2, paged.TotalCount);
        Assert.Equal(2, paged.Items.Count);
        Assert.All(paged.Items, l =>
        {
            Assert.Equal(userId, l.UserId);
            Assert.Equal("new", l.Status);
        });
    }

    [Fact]
    public async Task GetMyLeads_PaginationCalculatesCorrectPages()
    {
        var userId = Guid.NewGuid();
        var (controller, service) = CreateController(userId);

        for (int i = 0; i < 25; i++)
        {
            service.Leads.Add(new LeadDto(
                Guid.NewGuid(),
                userId,
                Guid.NewGuid(),
                $"Center {i}",
                null,
                "bachelor",
                "0912345678",
                $"Msg {i}",
                false,
                "new",
                DateTime.UtcNow.AddMinutes(i)));
        }

        // Lấy trang 2 với pageSize = 10
        var actionResult = await controller.GetMyLeads(new LeadQuery(Page: 2, PageSize: 10), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var paged = Assert.IsType<PagedResult<LeadDto>>(okResult.Value);

        Assert.Equal(25, paged.TotalCount);
        Assert.Equal(2, paged.Page);
        Assert.Equal(10, paged.PageSize);
        Assert.Equal(3, paged.TotalPages);
        Assert.Equal(10, paged.Items.Count);
    }
}

