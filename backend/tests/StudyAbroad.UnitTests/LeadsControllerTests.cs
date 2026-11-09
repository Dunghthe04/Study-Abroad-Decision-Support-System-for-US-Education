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
                throw new ArgumentException("MĂ£ trung tĂ¢m tÆ° váº¥n khĂ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng.");

            var lead = new LeadDto(
                Guid.NewGuid(),
                userId,
                request.StudyCenterId,
                "Trung TĂ¢m Du Há»c Test",
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

        public List<CenterReceivedLeadDto> ReceivedLeads { get; } = [];

        public Task<CenterLeadsSummaryDto> GetReceivedLeadsForCenterUserAsync(
            Guid userId,
            string? userEmail,
            string userRole,
            Guid? targetCenterId,
            string? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var items = ReceivedLeads.AsEnumerable();
            if (targetCenterId.HasValue && targetCenterId.Value != Guid.Empty)
            {
                items = items.Where(l => l.StudyCenterId == targetCenterId.Value);
            }
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(l => l.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }
            var list = items.ToList();
            return Task.FromResult(new CenterLeadsSummaryDto(
                list,
                list.Count,
                page,
                pageSize,
                list.Count(l => l.Status == "new"),
                list.Count(l => l.Status == "contacted"),
                list.Count(l => l.Status == "closed")));
        }

        public Task<CenterReceivedLeadDto?> GetReceivedLeadDetailForCenterUserAsync(
            Guid userId,
            string? userEmail,
            string userRole,
            Guid leadId,
            CancellationToken ct = default)
        {
            var lead = ReceivedLeads.FirstOrDefault(l => l.Id == leadId);
            return Task.FromResult(lead);
        }

        public Task<CenterReceivedLeadDto> UpdateReceivedLeadStatusAsync(
            Guid userId,
            string? userEmail,
            string userRole,
            Guid leadId,
            string newStatus,
            CancellationToken ct = default)
        {
            if (newStatus != "new" && newStatus != "contacted" && newStatus != "closed")
                throw new ArgumentException("Tráº¡ng thĂ¡i khĂ´ng há»£p lá»‡.");

            var lead = ReceivedLeads.FirstOrDefault(l => l.Id == leadId)
                ?? throw new KeyNotFoundException("KhĂ´ng tĂ¬m tháº¥y yĂªu cáº§u liĂªn há»‡.");

            var updated = lead with { Status = newStatus };
            var idx = ReceivedLeads.IndexOf(lead);
            ReceivedLeads[idx] = updated;
            return Task.FromResult(updated);
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

        var request = new CreateLeadRequest(centerId, "bachelor", "0912345678", "Em muá»‘n tÆ° váº¥n du há»c Má»¹", false);
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
        var (controller, _) = CreateController(userId, isMinor: true); // ChÆ°a Ä‘á»§ 18 tuá»•i

        var request = new CreateLeadRequest(centerId, "bachelor", "0912345678", "Em gá»­i há»“ sÆ¡", true);
        var result = await controller.CreateLead(request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateLead_AdultSharesProfile_Success_CreatesConsent()
    {
        var userId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var (controller, service) = CreateController(userId, isMinor: false); // Äá»§ 18 tuá»•i

        var request = new CreateLeadRequest(centerId, "master", "0912345678", "Em muá»‘n chia sáº» há»“ sÆ¡ há»c thuáº­t", true);
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

        // Seed leads cho user hiá»‡n táº¡i: 2 lead "new", 1 lead "contacted"
        service.Leads.Add(new LeadDto(Guid.NewGuid(), userId, Guid.NewGuid(), "Center A", null, "bachelor", "0912345678", "Msg 1", false, "new", DateTime.UtcNow.AddMinutes(-10)));
        service.Leads.Add(new LeadDto(Guid.NewGuid(), userId, Guid.NewGuid(), "Center B", null, "master", "0912345678", "Msg 2", false, "new", DateTime.UtcNow.AddMinutes(-5)));
        service.Leads.Add(new LeadDto(Guid.NewGuid(), userId, Guid.NewGuid(), "Center C", null, "bachelor", "0912345678", "Msg 3", false, "contacted", DateTime.UtcNow));

        // Seed lead cho user khĂ¡c (khĂ´ng Ä‘Æ°á»£c xuáº¥t hiá»‡n trong káº¿t quáº£)
        service.Leads.Add(new LeadDto(Guid.NewGuid(), otherUserId, Guid.NewGuid(), "Center D", null, "bachelor", "0999999999", "Other", false, "new", DateTime.UtcNow));

        // Act: Lá»c theo status "new"
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

        // Láº¥y trang 2 vá»›i pageSize = 10
        var actionResult = await controller.GetMyLeads(new LeadQuery(Page: 2, PageSize: 10), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var paged = Assert.IsType<PagedResult<LeadDto>>(okResult.Value);

        Assert.Equal(25, paged.TotalCount);
        Assert.Equal(2, paged.Page);
        Assert.Equal(10, paged.PageSize);
        Assert.Equal(3, paged.TotalPages);
        Assert.Equal(10, paged.Items.Count);
    }

    private static (LeadsController Controller, FakeLeadService Service) CreateCenterController(
        Guid userId,
        string role = UserRoles.Center,
        string email = "center@example.com")
    {
        var fakeLeadService = new FakeLeadService();
        var fakeAuthService = new FakeAuthService(true);
        var controller = new LeadsController(fakeLeadService, fakeAuthService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Email, email),
                        new Claim(ClaimTypes.Role, role),
                    }, "TestAuth"))
                }
            }
        };

        return (controller, fakeLeadService);
    }

    [Fact]
    public async Task GetReceivedLeads_AsCenter_ReturnsOkWithLeadsSummary()
    {
        var centerUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            Guid.NewGuid(),
            centerId,
            "Du Há»c Ă Ă‚u",
            "bachelor",
            "0912345678",
            "Xin tÆ° váº¥n há»c bá»•ng",
            false,
            "new",
            DateTime.UtcNow,
            null,
            null,
            null));

        var result = await controller.GetReceivedLeads(status: "all", page: 1, pageSize: 10, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<CenterLeadsSummaryDto>(ok.Value);
        Assert.Equal(1, summary.TotalCount);
        Assert.Equal(1, summary.NewCount);
        Assert.Single(summary.Items);
        Assert.Null(summary.Items[0].StudentName);
    }

    [Fact]
    public async Task GetReceivedLeadDetail_WhenNotShared_HidesProfileAndStudentEmail()
    {
        var centerUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Há»c Quá»‘c Táº¿",
            "master",
            "0987654321",
            "Cáº§n thĂ´ng tin chi phĂ­",
            false,
            "new",
            DateTime.UtcNow,
            null,
            null,
            null));

        var result = await controller.GetReceivedLeadDetail(leadId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<CenterReceivedLeadDto>(ok.Value);
        Assert.Equal(leadId, detail.Id);
        Assert.False(detail.SharedProfile);
        Assert.Null(detail.StudentName);
        Assert.Null(detail.StudentEmail);
        Assert.Null(detail.ProfileSummary);
        Assert.Equal("0987654321", detail.ContactPhone);
    }

    [Fact]
    public async Task GetReceivedLeadDetail_WhenShared_IncludesProfileAndStudentInfo()
    {
        var centerUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);

        var profile = new SharedProfileSummaryDto(
            3.85m,
            "bachelor",
            "Computer Science",
            "specialized",
            "Lá»›p 12",
            7.5m,
            "ielts",
            1480m,
            null,
            2,
            "tier_35k_50k");

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Há»c Quá»‘c Táº¿",
            "bachelor",
            "0987654321",
            "Em gá»­i há»“ sÆ¡ Ä‘áº§y Ä‘á»§",
            true,
            "new",
            DateTime.UtcNow,
            "Nguyen Van A",
            "student@example.com",
            profile));

        var result = await controller.GetReceivedLeadDetail(leadId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<CenterReceivedLeadDto>(ok.Value);
        Assert.True(detail.SharedProfile);
        Assert.Equal("Nguyen Van A", detail.StudentName);
        Assert.Equal("student@example.com", detail.StudentEmail);
        Assert.NotNull(detail.ProfileSummary);
        Assert.Equal(3.85m, detail.ProfileSummary.OverallGpa);
        Assert.Equal("Computer Science", detail.ProfileSummary.IntendedMajor);
    }

    [Fact]
    public async Task UpdateLeadStatus_WithValidStatus_ReturnsUpdatedLead()
    {
        var centerUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Há»c Test",
            "bachelor",
            "0911223344",
            "TÆ° váº¥n visa",
            false,
            "new",
            DateTime.UtcNow,
            null,
            null,
            null));

        var request = new UpdateLeadStatusRequest("contacted");
        var result = await controller.UpdateLeadStatus(leadId, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var updated = Assert.IsType<CenterReceivedLeadDto>(ok.Value);
        Assert.Equal("contacted", updated.Status);
    }

    [Fact]
    public async Task UpdateLeadStatus_WithInvalidStatus_ReturnsBadRequest()
    {
        var centerUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Há»c Test",
            "bachelor",
            "0911223344",
            "TÆ° váº¥n visa",
            false,
            "new",
            DateTime.UtcNow,
            null,
            null,
            null));

        var request = new UpdateLeadStatusRequest("invalid_status");
        var result = await controller.UpdateLeadStatus(leadId, request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
    }
}

