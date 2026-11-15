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
        /// <summary>Nếu đặt, CreateLeadAsync ném lỗi này (giả lập service từ chối theo quy tắc UC-42).</summary>
        public LeadRuleException? RuleToThrow { get; set; }

        public Task<LeadDto> CreateLeadAsync(Guid userId, CreateLeadRequest request, string? clientIp = null, CancellationToken ct = default)
        {
            if (RuleToThrow is not null)
                throw RuleToThrow;
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

        // [UC-44] RuleToThrow giả lập service từ chối (vd. học sinh dưới 16)
        public List<SentLeadDto> SentLeads { get; } = [];

        public Task<PagedResult<SentLeadDto>> GetSentLeadsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            if (RuleToThrow is not null)
                throw RuleToThrow;
            return Task.FromResult(new PagedResult<SentLeadDto>(SentLeads, page, pageSize, SentLeads.Count));
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
                throw new ArgumentException("Trạng thái không hợp lệ.");

            var lead = ReceivedLeads.FirstOrDefault(l => l.Id == leadId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

            var updated = lead with { Status = newStatus };
            var idx = ReceivedLeads.IndexOf(lead);
            ReceivedLeads[idx] = updated;
            return Task.FromResult(updated);
        }

        public Dictionary<Guid, List<LeadMessageDto>> MessagesByLead { get; } = [];
        public Guid? ForbiddenLeadId { get; set; }

        public Task<LeadMessagesThreadDto> GetReceivedLeadMessagesForCenterUserAsync(
            Guid userId,
            string? userEmail,
            string userRole,
            Guid leadId,
            CancellationToken ct = default)
        {
            if (ForbiddenLeadId == leadId)
                throw new UnauthorizedAccessException("Forbidden");

            var lead = ReceivedLeads.FirstOrDefault(l => l.Id == leadId)
                ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

            var list = MessagesByLead.TryGetValue(leadId, out var msgs) ? msgs : [];
            return Task.FromResult(new LeadMessagesThreadDto(
                leadId,
                lead.Status,
                list,
                list.Count(m => m.IsFlagged && m.Status != "removed")));
        }

        public Task<SendLeadMessageResultDto> SendLeadMessageForCenterUserAsync(
            Guid userId,
            string? userEmail,
            string userRole,
            Guid leadId,
            string content,
            CancellationToken ct = default)
        {
            if (leadId == Guid.Empty)
                throw new ArgumentException("Mã yêu cầu liên hệ không hợp lệ.");
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Nội dung tin nhắn không được để trống.");
            if (content.Trim().Length > 4000)
                throw new ArgumentException("Nội dung tin nhắn tối đa 4000 ký tự.");
            if (ForbiddenLeadId == leadId)
                throw new UnauthorizedAccessException("Forbidden");

            var idx = ReceivedLeads.FindIndex(l => l.Id == leadId);
            if (idx < 0)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");

            var lead = ReceivedLeads[idx];
            var nextStatus = lead.Status == "new" ? "contacted" : lead.Status;
            ReceivedLeads[idx] = lead with { Status = nextStatus };

            var filter = LeadMessageContentFilter.Inspect(content);
            var senderName = LeadMessageMapper.ResolveSenderDisplayName(userRole, null, lead.SharedProfile, lead.StudyCenterName);
            var msg = new LeadMessageDto(
                Guid.NewGuid(),
                leadId,
                userId,
                senderName,
                userRole,
                true,
                content.Trim(),
                filter.IsFlagged,
                filter.FlagReason,
                "visible",
                DateTime.UtcNow);

            if (!MessagesByLead.TryGetValue(leadId, out var list))
            {
                list = [];
                MessagesByLead[leadId] = list;
            }
            list.Add(msg);

            return Task.FromResult(new SendLeadMessageResultDto(msg, nextStatus));
        }

        // [Nhắn tin phía người gửi] Lead không có quyền / không tồn tại → service ném KeyNotFoundException
        public Guid? MissingSentLeadId { get; set; }

        public Task<LeadMessagesThreadDto> GetSentLeadMessagesAsync(Guid userId, Guid leadId, CancellationToken ct = default)
        {
            if (RuleToThrow is not null)
                throw RuleToThrow;
            if (MissingSentLeadId == leadId)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");
            var messages = MessagesByLead.TryGetValue(leadId, out var list) ? list : [];
            return Task.FromResult(new LeadMessagesThreadDto(leadId, "contacted", messages, 0));
        }

        public Task<SendLeadMessageResultDto> SendSentLeadMessageAsync(Guid userId, Guid leadId, string content, CancellationToken ct = default)
        {
            if (RuleToThrow is not null)
                throw RuleToThrow;
            if (MissingSentLeadId == leadId)
                throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ.");
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Nội dung tin nhắn không được để trống.");
            var msg = new LeadMessageDto(
                Guid.NewGuid(), leadId, userId, "Bạn", UserRoles.Student, false,
                content.Trim(), false, null, "visible", DateTime.UtcNow);
            return Task.FromResult(new SendLeadMessageResultDto(msg, "contacted"));
        }
    }

    private static (LeadsController Controller, FakeLeadService Service) CreateController(Guid userId)
    {
        var fakeLeadService = new FakeLeadService();
        var controller = new LeadsController(fakeLeadService)
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
        var (controller, service) = CreateController(userId);

        var request = new CreateLeadRequest(centerId, "bachelor", "0912345678", "Em muốn tư vấn du học Mỹ", false);
        var result = await controller.CreateLead(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<LeadDto>(created.Value);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal(centerId, dto.StudyCenterId);
        Assert.False(dto.SharedProfile);
        Assert.Null(dto.ConsentId);
    }

    /// <summary>Đọc thuộc tính <paramref name="name"/> của body ẩn danh { code, message, retryAt }.</summary>
    private static object? BodyValue(ObjectResult result, string name) =>
        result.Value!.GetType().GetProperty(name)?.GetValue(result.Value);

    [Theory]
    [InlineData("age_restricted", StatusCodes.Status403Forbidden)]
    [InlineData("not_linked", StatusCodes.Status403Forbidden)]
    [InlineData("daily_limit", StatusCodes.Status429TooManyRequests)]
    [InlineData("consent_required", StatusCodes.Status400BadRequest)]
    [InlineData("acknowledgement_required", StatusCodes.Status400BadRequest)]
    public async Task CreateLead_ServiceRejectsByRule_ReturnsStatusAndCode(string code, int status)
    {
        var (controller, service) = CreateController(Guid.NewGuid());
        service.RuleToThrow = new LeadRuleException(code, status, "Không gửi được.");

        var result = await controller.CreateLead(new CreateLeadRequest(Guid.NewGuid()), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, obj.StatusCode);
        Assert.Equal(code, BodyValue(obj, "code"));
        Assert.Equal("Không gửi được.", BodyValue(obj, "message"));
        Assert.Null(BodyValue(obj, "retryAt"));
    }

    [Fact]
    public async Task CreateLead_CenterCooldown_Returns429WithRetryAt()
    {
        var retryAt = new DateTime(2026, 11, 19, 3, 0, 0, DateTimeKind.Utc);
        var (controller, service) = CreateController(Guid.NewGuid());
        service.RuleToThrow = new LeadRuleException("center_cooldown", StatusCodes.Status429TooManyRequests, "Đợi 7 ngày.", retryAt);

        var result = await controller.CreateLead(new CreateLeadRequest(Guid.NewGuid()), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, obj.StatusCode);
        Assert.Equal("center_cooldown", BodyValue(obj, "code"));
        Assert.Equal(retryAt, BodyValue(obj, "retryAt"));
    }

    [Fact]
    public async Task CreateLead_AdultSharesProfile_Success_CreatesConsent()
    {
        var userId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var (controller, service) = CreateController(userId);

        var request = new CreateLeadRequest(centerId, "master", "0912345678", "Em muốn chia sẻ hồ sơ học thuật", true);
        var result = await controller.CreateLead(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<LeadDto>(created.Value);
        Assert.True(dto.SharedProfile);
        Assert.NotNull(dto.ConsentId);
    }

    // ===== UC-44: xem yêu cầu liên hệ đã gửi =====

    [Fact]
    public async Task GetSentLeads_ReturnsOkWithPagedLeads()
    {
        var (controller, service) = CreateController(Guid.NewGuid());
        service.SentLeads.Add(new SentLeadDto(
            Guid.NewGuid(), Guid.NewGuid(), "Trung Tâm Test", "new", DateTime.UtcNow, [LeadSharedFields.Contact],
            "bachelor", "0912345678", null, Guid.NewGuid(), "Học Sinh", SubmittedByParent: false, CenterMessageCount: 2));

        var result = await controller.GetSentLeads(page: 2, pageSize: 5);

        var ok = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResult<SentLeadDto>>(ok.Value);
        Assert.Equal(2, paged.Page);
        Assert.Equal(5, paged.PageSize);
        Assert.Equal(2, Assert.Single(paged.Items).CenterMessageCount);
    }

    [Theory]
    [InlineData("age_restricted")]
    [InlineData("role_not_allowed")]
    public async Task GetSentLeads_ServiceRejects_Returns403WithCode(string code)
    {
        var (controller, service) = CreateController(Guid.NewGuid());
        service.RuleToThrow = new LeadRuleException(code, StatusCodes.Status403Forbidden, "Không xem được.");

        var result = await controller.GetSentLeads();

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        Assert.Equal(code, BodyValue(obj, "code"));
        Assert.Equal("Không xem được.", BodyValue(obj, "message"));
    }

    [Fact]
    public async Task GetSentLeads_Unauthenticated_ReturnsUnauthorized()
    {
        var controller = new LeadsController(new FakeLeadService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.IsType<UnauthorizedObjectResult>(await controller.GetSentLeads());
    }

    [Fact]
    public void GetSentLeads_AuthorizesStudentAndParentOnly()
    {
        var attr = typeof(LeadsController)
            .GetMethod(nameof(LeadsController.GetSentLeads))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal($"{UserRoles.Student},{UserRoles.Parent}", attr.Roles);
    }

    // ===== Nhắn tin phía người gửi: GET/POST /leads/sent/{id}/messages =====

    [Fact]
    public async Task GetSentLeadMessages_ReturnsOkWithThread()
    {
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateController(Guid.NewGuid());
        service.MessagesByLead[leadId] =
        [
            new LeadMessageDto(Guid.NewGuid(), leadId, Guid.NewGuid(), "Trung Tâm Test", UserRoles.Center, true,
                "Chào em", false, null, "visible", DateTime.UtcNow),
        ];

        var result = await controller.GetSentLeadMessages(leadId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var thread = Assert.IsType<LeadMessagesThreadDto>(ok.Value);
        Assert.Equal(leadId, thread.LeadId);
        Assert.Equal("Chào em", Assert.Single(thread.Messages).Content);
    }

    [Fact]
    public async Task SendSentLeadMessage_ReturnsCreatedWithMessage()
    {
        var userId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, _) = CreateController(userId);

        var result = await controller.SendSentLeadMessage(leadId, new SendLeadMessageRequest("  Em cảm ơn  "), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(LeadsController.GetSentLeadMessages), created.ActionName);
        var dto = Assert.IsType<SendLeadMessageResultDto>(created.Value);
        Assert.Equal("Em cảm ơn", dto.Message.Content);
        Assert.Equal(userId, dto.Message.SenderUserId);
        Assert.False(dto.Message.IsFromCenter);
    }

    [Theory]
    [InlineData("age_restricted")]
    [InlineData("role_not_allowed")]
    public async Task SentLeadMessages_ServiceRejects_Returns403WithCode(string code)
    {
        var (controller, service) = CreateController(Guid.NewGuid());
        service.RuleToThrow = new LeadRuleException(code, StatusCodes.Status403Forbidden, "Không xem được.");

        var get = Assert.IsType<ObjectResult>(await controller.GetSentLeadMessages(Guid.NewGuid(), CancellationToken.None));
        var post = Assert.IsType<ObjectResult>(await controller.SendSentLeadMessage(
            Guid.NewGuid(), new SendLeadMessageRequest("Chào"), CancellationToken.None));

        foreach (var obj in new[] { get, post })
        {
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
            Assert.Equal(code, BodyValue(obj, "code"));
        }
    }

    [Fact]
    public async Task SendSentLeadMessage_ClosedLead_Returns400LeadClosed()
    {
        var (controller, service) = CreateController(Guid.NewGuid());
        service.RuleToThrow = new LeadRuleException("lead_closed", StatusCodes.Status400BadRequest, "Yêu cầu đã hoàn tất.");

        var result = await controller.SendSentLeadMessage(Guid.NewGuid(), new SendLeadMessageRequest("Chào"), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, obj.StatusCode);
        Assert.Equal("lead_closed", BodyValue(obj, "code"));
    }

    [Fact]
    public async Task SentLeadMessages_LeadNotFoundOrNotOwned_Returns404()
    {
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateController(Guid.NewGuid());
        service.MissingSentLeadId = leadId;

        var get = Assert.IsType<NotFoundObjectResult>(await controller.GetSentLeadMessages(leadId, CancellationToken.None));
        var post = Assert.IsType<NotFoundObjectResult>(await controller.SendSentLeadMessage(
            leadId, new SendLeadMessageRequest("Chào"), CancellationToken.None));

        Assert.Equal("not_found", BodyValue(get, "code"));
        Assert.Equal("not_found", BodyValue(post, "code"));
    }

    [Fact]
    public async Task SendSentLeadMessage_EmptyContent_Returns400()
    {
        var (controller, _) = CreateController(Guid.NewGuid());

        var result = await controller.SendSentLeadMessage(Guid.NewGuid(), new SendLeadMessageRequest("   "), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("invalid_content", BodyValue(bad, "code"));
    }

    [Fact]
    public async Task SentLeadMessages_Unauthenticated_ReturnsUnauthorized()
    {
        var controller = new LeadsController(new FakeLeadService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.IsType<UnauthorizedObjectResult>(await controller.GetSentLeadMessages(Guid.NewGuid(), CancellationToken.None));
        Assert.IsType<UnauthorizedObjectResult>(await controller.SendSentLeadMessage(
            Guid.NewGuid(), new SendLeadMessageRequest("Chào"), CancellationToken.None));
    }

    [Theory]
    [InlineData(nameof(LeadsController.GetSentLeadMessages))]
    [InlineData(nameof(LeadsController.SendSentLeadMessage))]
    public void SentLeadMessages_AuthorizesStudentAndParentOnly(string method)
    {
        var attr = typeof(LeadsController)
            .GetMethod(method)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal($"{UserRoles.Student},{UserRoles.Parent}", attr.Roles);
    }

    private static (LeadsController Controller, FakeLeadService Service) CreateCenterController(
        Guid userId,
        string role = UserRoles.Center,
        string email = "center@example.com")
    {
        var fakeLeadService = new FakeLeadService();
        var controller = new LeadsController(fakeLeadService)
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
            "Du Học Á Âu",
            "bachelor",
            "0912345678",
            "Xin tư vấn học bổng",
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
            "Du Học Quốc Tế",
            "master",
            "0987654321",
            "Cần thông tin chi phí",
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
            "Lớp 12",
            7.5m,
            "ielts",
            1480m,
            null,
            2,
            "tier_35k_50k");

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Học Quốc Tế",
            "bachelor",
            "0987654321",
            "Em gửi hồ sơ đầy đủ",
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
            "Du Học Test",
            "bachelor",
            "0911223344",
            "Tư vấn visa",
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
            "Du Học Test",
            "bachelor",
            "0911223344",
            "Tư vấn visa",
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

    [Fact]
    public async Task GetReceivedLeadMessages_ValidLead_ReturnsOkWithThread()
    {
        var centerUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Học Á Âu",
            "bachelor",
            "0912345678",
            "Xin tư vấn",
            false,
            "contacted",
            DateTime.UtcNow,
            null,
            null,
            null));

        service.MessagesByLead[leadId] =
        [
            new LeadMessageDto(
                Guid.NewGuid(),
                leadId,
                centerUserId,
                "Du Học Á Âu",
                UserRoles.Center,
                true,
                "Chào em, trung tâm đã xem hồ sơ.",
                false,
                null,
                "visible",
                DateTime.UtcNow)
        ];

        var result = await controller.GetReceivedLeadMessages(leadId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var thread = Assert.IsType<LeadMessagesThreadDto>(ok.Value);
        Assert.Equal(leadId, thread.LeadId);
        Assert.Single(thread.Messages);
    }

    [Fact]
    public async Task SendReceivedLeadMessage_ValidReply_ReturnsCreatedAndUpdatesLeadStatus()
    {
        var centerUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Học Á Âu",
            "bachelor",
            null,
            "Tư vấn giúp em",
            true,
            "new",
            DateTime.UtcNow,
            "Nguyen Van A",
            "a@example.com",
            null));

        var result = await controller.SendReceivedLeadMessage(
            leadId,
            new SendLeadMessageRequest("Trung tâm gửi em lộ trình chi tiết."),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<SendLeadMessageResultDto>(created.Value);
        Assert.Equal("contacted", payload.LeadStatus);
        Assert.False(payload.Message.IsFlagged);
        Assert.Equal("Trung tâm gửi em lộ trình chi tiết.", payload.Message.Content);
    }

    [Fact]
    public async Task SendReceivedLeadMessage_EmptyContent_ReturnsValidationProblem()
    {
        var centerUserId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, _) = CreateCenterController(centerUserId);

        var result = await controller.SendReceivedLeadMessage(
            leadId,
            new SendLeadMessageRequest("   "),
            CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
    }

    [Fact]
    public async Task GetAndSendLeadMessage_ForbiddenCenter_ReturnsForbid()
    {
        var centerUserId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);
        service.ForbiddenLeadId = leadId;

        var getResult = await controller.GetReceivedLeadMessages(leadId, CancellationToken.None);
        var postResult = await controller.SendReceivedLeadMessage(
            leadId,
            new SendLeadMessageRequest("Hello"),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(getResult);
        Assert.IsType<ForbidResult>(postResult);
    }

    [Fact]
    public async Task GetReceivedLeadMessages_NotFound_ReturnsNotFound()
    {
        var centerUserId = Guid.NewGuid();
        var (controller, _) = CreateCenterController(centerUserId);

        var result = await controller.GetReceivedLeadMessages(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void MessageEndpoints_AuthorizeCenterModeratorAndAdminRoles()
    {
        var getAttr = typeof(LeadsController)
            .GetMethod(nameof(LeadsController.GetReceivedLeadMessages))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .Single();

        var postAttr = typeof(LeadsController)
            .GetMethod(nameof(LeadsController.SendReceivedLeadMessage))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .Single();

        var expectedRoles = $"{UserRoles.Center},{UserRoles.Moderator},{UserRoles.Admin}";
        Assert.Equal(expectedRoles, getAttr.Roles);
        Assert.Equal(expectedRoles, postAttr.Roles);
    }

    [Fact]
    public async Task GetAndSendLeadMessages_Unauthenticated_ReturnsUnauthorized()
    {
        var controller = new LeadsController(new FakeLeadService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var leadId = Guid.NewGuid();
        var getResult = await controller.GetReceivedLeadMessages(leadId, CancellationToken.None);
        var postResult = await controller.SendReceivedLeadMessage(leadId, new SendLeadMessageRequest("Xin chào"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(getResult);
        Assert.IsType<UnauthorizedObjectResult>(postResult);
    }

    [Fact]
    public async Task SendReceivedLeadMessage_WithPhoneAndPromo_ReturnsCreatedWithFlaggedPayload()
    {
        var centerUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(centerUserId);

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Học Á Âu",
            "bachelor",
            null,
            "Em hỏi về học bổng",
            true,
            "new",
            DateTime.UtcNow,
            "Nguyen Van A",
            "a@example.com",
            null));

        var result = await controller.SendReceivedLeadMessage(
            leadId,
            new SendLeadMessageRequest("Nhắn qua zalo 0912 345 678 để nhận khuyến mãi nhé"),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var payload = Assert.IsType<SendLeadMessageResultDto>(created.Value);
        Assert.True(payload.Message.IsFlagged);
        Assert.NotNull(payload.Message.FlagReason);
        Assert.Equal("contacted", payload.LeadStatus);
    }

    [Fact]
    public async Task SendReceivedLeadMessage_EmptyGuid_ReturnsValidationProblem()
    {
        var centerUserId = Guid.NewGuid();
        var (controller, _) = CreateCenterController(centerUserId);

        var result = await controller.SendReceivedLeadMessage(
            Guid.Empty,
            new SendLeadMessageRequest("Nội dung hợp lệ"),
            CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
    }

    [Theory]
    [InlineData(UserRoles.Moderator, "Kiểm duyệt viên")]
    [InlineData(UserRoles.Admin, "Quản trị viên")]
    public async Task SendAndGetLeadMessages_AsModeratorOrAdmin_SucceedsWithStaffName(string role, string expectedSenderName)
    {
        var staffUserId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var (controller, service) = CreateCenterController(staffUserId, role: role, email: "staff@usas.vn");

        service.ReceivedLeads.Add(new CenterReceivedLeadDto(
            leadId,
            centerId,
            "Du Học Test",
            "bachelor",
            null,
            "Kiểm tra yêu cầu",
            false,
            "new",
            DateTime.UtcNow,
            null,
            null,
            null));

        var postResult = await controller.SendReceivedLeadMessage(
            leadId,
            new SendLeadMessageRequest("Tin nhắn hỗ trợ từ hệ thống."),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(postResult);
        var payload = Assert.IsType<SendLeadMessageResultDto>(created.Value);
        Assert.Equal(expectedSenderName, payload.Message.SenderName);

        var getResult = await controller.GetReceivedLeadMessages(leadId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(getResult);
        var thread = Assert.IsType<LeadMessagesThreadDto>(ok.Value);
        Assert.Single(thread.Messages);
    }
}

