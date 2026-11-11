using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class LeadServiceTests
{
    private sealed class FakeStudyCenterRepo : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];

        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task AddAsync(StudyCenter center, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default)
        {
            var byOwner = Centers.FirstOrDefault(c => c.OwnerUserId == ownerUserId);
            if (byOwner != null) return Task.FromResult<StudyCenter?>(byOwner);

            if (!string.IsNullOrWhiteSpace(email))
            {
                var byEmail = Centers.FirstOrDefault(c => c.Email != null && string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(byEmail);
            }

            return Task.FromResult<StudyCenter?>(null);
        }
    }

    private sealed class FakeAuditRepo : IAuditLogRepository
    {
        public List<AuditLog> Logs { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            Logs.Add(entry);
            return Task.CompletedTask;
        }

        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeLeadRepo : ILeadRepository
    {
        public List<Lead> Leads { get; } = [];
        public List<LeadMessage> Messages { get; } = [];
        public Dictionary<Guid, (string FullName, string Role)> Users { get; } = [];

        public Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Leads.FirstOrDefault(l => l.Id == id));

        public Task<LeadDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Lead> CreateLeadAsync(Lead lead, Consent? consent = null, CancellationToken ct = default)
        {
            Leads.Add(lead);
            return Task.FromResult(lead);
        }

        public Task<CenterLeadsSummaryDto> GetReceivedLeadsAsync(
            Guid? centerId,
            string? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var q = Leads.AsEnumerable();
            if (centerId.HasValue && centerId.Value != Guid.Empty)
                q = q.Where(l => l.StudyCenterId == centerId.Value);

            var total = q.Count();
            var newC = q.Count(l => l.Status == "new");
            var contactedC = q.Count(l => l.Status == "contacted");
            var closedC = q.Count(l => l.Status == "closed");

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
                q = q.Where(l => l.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

            var items = q.Select(l => new CenterReceivedLeadDto(
                l.Id,
                l.StudyCenterId,
                "Test Center",
                l.StudyLevel,
                l.ContactPhone,
                l.Message,
                l.SharedProfile,
                l.Status,
                l.CreatedAt,
                l.SharedProfile ? "Student Name" : null,
                l.SharedProfile ? "student@example.com" : null,
                null)).ToList();

            return Task.FromResult(new CenterLeadsSummaryDto(
                items,
                items.Count,
                page,
                pageSize,
                newC,
                contactedC,
                closedC));
        }

        public Task<CenterReceivedLeadDto?> GetReceivedLeadDetailAsync(
            Guid leadId,
            Guid? restrictToCenterId,
            CancellationToken ct = default)
        {
            var lead = Leads.FirstOrDefault(l => l.Id == leadId);
            if (lead == null) return Task.FromResult<CenterReceivedLeadDto?>(null);

            if (restrictToCenterId.HasValue && lead.StudyCenterId != restrictToCenterId.Value)
                return Task.FromResult<CenterReceivedLeadDto?>(null);

            return Task.FromResult<CenterReceivedLeadDto?>(new CenterReceivedLeadDto(
                lead.Id,
                lead.StudyCenterId,
                "Test Center",
                lead.StudyLevel,
                lead.ContactPhone,
                lead.Message,
                lead.SharedProfile,
                lead.Status,
                lead.CreatedAt,
                lead.SharedProfile ? "Student Name" : null,
                lead.SharedProfile ? "student@example.com" : null,
                null));
        }

        public Task<Lead?> GetLeadForUpdateAsync(Guid leadId, CancellationToken ct = default) =>
            Task.FromResult(Leads.FirstOrDefault(l => l.Id == leadId));

        public Task UpdateAsync(Lead lead, CancellationToken ct = default)
        {
            var idx = Leads.FindIndex(l => l.Id == lead.Id);
            if (idx >= 0) Leads[idx] = lead;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LeadMessageDto>> GetLeadMessagesAsync(
            Guid leadId,
            Guid leadOwnerUserId,
            bool sharedProfile,
            string centerName,
            CancellationToken ct = default)
        {
            var list = Messages
                .Where(m => m.LeadId == leadId)
                .OrderBy(m => m.CreatedAt)
                .Select(m =>
                {
                    var (fullName, role) = Users.TryGetValue(m.SenderUserId, out var u)
                        ? u
                        : ("Người dùng", UserRoles.Student);

                    return LeadMessageMapper.ToDto(m, role, fullName, sharedProfile, centerName);
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<LeadMessageDto>>(list);
        }

        public Task<LeadMessage> AddLeadMessageAsync(
            LeadMessage message,
            Lead? leadToUpdate = null,
            CancellationToken ct = default)
        {
            Messages.Add(message);
            if (leadToUpdate is not null)
            {
                var idx = Leads.FindIndex(l => l.Id == leadToUpdate.Id);
                if (idx >= 0) Leads[idx] = leadToUpdate;
            }
            return Task.FromResult(message);
        }
    }

    [Fact]
    public async Task GetReceivedLeads_CenterUser_FiltersByCenter()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm A", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = Guid.NewGuid(), StudyCenterId = centerId, Status = "new", ContactPhone = "0912345678" });
        leadRepo.Leads.Add(new Lead { Id = Guid.NewGuid(), StudyCenterId = Guid.NewGuid(), Status = "new", ContactPhone = "0987654321" });

        var service = new LeadService(leadRepo, centerRepo);

        var result = await service.GetReceivedLeadsForCenterUserAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            null,
            "all",
            null,
            1,
            10);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("0912345678", result.Items[0].ContactPhone);
    }

    [Fact]
    public async Task GetReceivedLeads_CenterUserWithoutCenter_ThrowsKeyNotFoundException()
    {
        var unlinkedUserId = Guid.NewGuid();
        var centerRepo = new FakeStudyCenterRepo();
        var leadRepo = new FakeLeadRepo();
        var service = new LeadService(leadRepo, centerRepo);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetReceivedLeadsForCenterUserAsync(
                unlinkedUserId,
                "unlinked@example.com",
                UserRoles.Center,
                null,
                "all",
                null,
                1,
                10));
    }

    [Fact]
    public async Task UpdateReceivedLeadStatus_ValidStatus_UpdatesAndReturnsNewStatus()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm B", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, StudyCenterId = centerId, Status = "new", ContactPhone = "0912345678" });

        var service = new LeadService(leadRepo, centerRepo);

        var updated = await service.UpdateReceivedLeadStatusAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            leadId,
            "contacted");

        Assert.Equal("contacted", updated.Status);
    }

    [Fact]
    public async Task UpdateReceivedLeadStatus_InvalidStatus_ThrowsArgumentException()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm C", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, StudyCenterId = centerId, Status = "new" });

        var service = new LeadService(leadRepo, centerRepo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateReceivedLeadStatusAsync(
                centerOwnerId,
                "owner@center.com",
                UserRoles.Center,
                leadId,
                "hacker_status"));
    }

    [Fact]
    public async Task UpdateReceivedLeadStatus_AnotherCenterLead_ThrowsUnauthorizedAccessException()
    {
        var centerOwnerId = Guid.NewGuid();
        var myCenterId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = myCenterId, Name = "Trung Tâm Của Tôi", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, StudyCenterId = otherCenterId, Status = "new" });

        var service = new LeadService(leadRepo, centerRepo);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateReceivedLeadStatusAsync(
                centerOwnerId,
                "owner@center.com",
                UserRoles.Center,
                leadId,
                "contacted"));
    }

    [Fact]
    public async Task SendLeadMessage_ValidCleanReply_SavesMessageUpdatesNewLeadToContactedAndLogsAudit()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Minh bạch", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = studentId, StudyCenterId = centerId, Status = "new" });

        var auditRepo = new FakeAuditRepo();
        var service = new LeadService(leadRepo, centerRepo, auditRepo);

        var res = await service.SendLeadMessageForCenterUserAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            leadId,
            "  Chào em, trung tâm đã nhận hồ sơ và sẽ tư vấn lộ trình phù hợp.  ");

        Assert.Equal("contacted", res.LeadStatus);
        Assert.Equal("contacted", leadRepo.Leads[0].Status);
        Assert.False(res.Message.IsFlagged);
        Assert.Null(res.Message.FlagReason);
        Assert.Equal("visible", res.Message.Status);
        Assert.Equal("Trung Tâm Minh bạch", res.Message.SenderName);
        Assert.Equal("Chào em, trung tâm đã nhận hồ sơ và sẽ tư vấn lộ trình phù hợp.", res.Message.Content);
        Assert.Single(leadRepo.Messages);
        Assert.Single(auditRepo.Logs);
        Assert.Equal("lead_message.send", auditRepo.Logs[0].Action);
    }

    [Fact]
    public async Task SendLeadMessage_WithPhoneAndPromo_FlagsMessageAndRecordsFlaggedAudit()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Test", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = "contacted" });

        var auditRepo = new FakeAuditRepo();
        var service = new LeadService(leadRepo, centerRepo, auditRepo);

        var res = await service.SendLeadMessageForCenterUserAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            leadId,
            "Nhắn qua số 0912345678 để nhận khuyến mãi nhé!");

        Assert.True(res.Message.IsFlagged);
        Assert.NotNull(res.Message.FlagReason);
        Assert.Contains("số điện thoại", res.Message.FlagReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quảng cáo", res.Message.FlagReason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(leadRepo.Messages);
        Assert.True(leadRepo.Messages[0].IsFlagged);
        Assert.Single(auditRepo.Logs);
        Assert.Equal("lead_message.send_flagged", auditRepo.Logs[0].Action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendLeadMessage_EmptyContent_ThrowsArgumentException(string content)
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Test", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = "new" });

        var service = new LeadService(leadRepo, centerRepo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendLeadMessageForCenterUserAsync(centerOwnerId, "owner@center.com", UserRoles.Center, leadId, content));
    }

    [Fact]
    public async Task SendLeadMessage_ExceedsMaxLength_ThrowsArgumentException()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Test", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = "new" });

        var service = new LeadService(leadRepo, centerRepo);
        var tooLong = new string('a', LeadService.MaxReplyMessageLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendLeadMessageForCenterUserAsync(centerOwnerId, "owner@center.com", UserRoles.Center, leadId, tooLong));
    }

    [Fact]
    public async Task GetAndSendLeadMessage_AnotherCenterLead_ThrowsUnauthorizedAccessException()
    {
        var centerOwnerId = Guid.NewGuid();
        var myCenterId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = myCenterId, Name = "Trung Tâm A", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = otherCenterId, Status = "new" });

        var service = new LeadService(leadRepo, centerRepo);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetReceivedLeadMessagesForCenterUserAsync(centerOwnerId, "a@center.com", UserRoles.Center, leadId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SendLeadMessageForCenterUserAsync(centerOwnerId, "a@center.com", UserRoles.Center, leadId, "Xin chào"));
    }

    [Fact]
    public async Task GetReceivedLeadMessages_MasksStudentNameWhenNotSharedAndMasksRemovedContent()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Bảo Mật", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Users[studentId] = ("Nguyen Van Bi Mat", UserRoles.Student);
        leadRepo.Users[centerOwnerId] = ("Tư vấn viên", UserRoles.Center);
        leadRepo.Leads.Add(new Lead
        {
            Id = leadId,
            UserId = studentId,
            StudyCenterId = centerId,
            SharedProfile = false,
            Status = "contacted",
        });

        leadRepo.Messages.Add(new LeadMessage
        {
            LeadId = leadId,
            SenderUserId = studentId,
            Content = "Em muốn hỏi học bổng",
            Status = "visible",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
        });
        leadRepo.Messages.Add(new LeadMessage
        {
            LeadId = leadId,
            SenderUserId = centerOwnerId,
            Content = "Số điện thoại bị gỡ",
            IsFlagged = true,
            FlagReason = "Chứa số điện thoại liên hệ",
            Status = "removed",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        });
        leadRepo.Messages.Add(new LeadMessage
        {
            LeadId = leadId,
            SenderUserId = centerOwnerId,
            Content = "Chào em, vui lòng xem lộ trình gợi ý nhé.",
            IsFlagged = false,
            Status = "visible",
            CreatedAt = DateTime.UtcNow,
        });

        var service = new LeadService(leadRepo, centerRepo);

        var thread = await service.GetReceivedLeadMessagesForCenterUserAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            leadId);

        Assert.Equal(3, thread.Messages.Count);
        Assert.Equal("Học sinh / Phụ huynh (Ẩn danh)", thread.Messages[0].SenderName);
        Assert.False(thread.Messages[0].IsFromCenter);
        Assert.Equal("Tin nhắn đã bị gỡ do vi phạm quy tắc.", thread.Messages[1].Content);
        Assert.Null(thread.Messages[1].FlagReason);
        Assert.Equal("Trung Tâm Bảo Mật", thread.Messages[2].SenderName);
        Assert.True(thread.Messages[2].IsFromCenter);
        Assert.Equal(0, thread.FlaggedCount);
    }

    [Fact]
    public async Task GetReceivedLeadMessages_WhenSharedProfileTrue_ShowsStudentAndParentRealNames()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Hợp Tác", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Users[studentId] = ("Lê Hoàng Minh", UserRoles.Student);
        leadRepo.Users[parentId] = ("Phạm Thu Hương", UserRoles.Parent);
        leadRepo.Leads.Add(new Lead
        {
            Id = leadId,
            UserId = studentId,
            StudyCenterId = centerId,
            SharedProfile = true,
            Status = "new",
        });

        leadRepo.Messages.Add(new LeadMessage
        {
            LeadId = leadId,
            SenderUserId = studentId,
            Content = "Em hỏi về học bổng kỹ thuật",
            Status = "visible",
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
        });
        leadRepo.Messages.Add(new LeadMessage
        {
            LeadId = leadId,
            SenderUserId = parentId,
            Content = "Gia đình cần tư vấn thêm ký túc xá",
            Status = "visible",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
        });

        var service = new LeadService(leadRepo, centerRepo);

        var thread = await service.GetReceivedLeadMessagesForCenterUserAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            leadId);

        Assert.Equal(2, thread.Messages.Count);
        Assert.Equal("Lê Hoàng Minh", thread.Messages[0].SenderName);
        Assert.False(thread.Messages[0].IsFromCenter);
        Assert.Equal("Phạm Thu Hương", thread.Messages[1].SenderName);
        Assert.False(thread.Messages[1].IsFromCenter);
    }

    [Theory]
    [InlineData("contacted")]
    [InlineData("closed")]
    public async Task SendLeadMessage_OnContactedOrClosedLead_PreservesExistingStatus(string initialStatus)
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Test", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = initialStatus });

        var service = new LeadService(leadRepo, centerRepo);

        var result = await service.SendLeadMessageForCenterUserAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            leadId,
            "Trung tâm xác nhận lại lịch tư vấn tuần này.");

        Assert.Equal(initialStatus, result.LeadStatus);
        Assert.Equal(initialStatus, leadRepo.Leads[0].Status);
    }

    [Theory]
    [InlineData(UserRoles.Moderator, "Kiểm duyệt viên")]
    [InlineData(UserRoles.Admin, "Quản trị viên")]
    public async Task SendLeadMessage_AsModeratorOrAdmin_UsesStaffDisplayNameAndIsFromCenter(string role, string expectedName)
    {
        var staffId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Quốc Tế", OwnerUserId = Guid.NewGuid() });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = "new" });

        var service = new LeadService(leadRepo, centerRepo);

        var result = await service.SendLeadMessageForCenterUserAsync(
            staffId,
            "staff@usas.vn",
            role,
            leadId,
            "Hệ thống hỗ trợ kiểm tra hồ sơ liên hệ.");

        Assert.Equal(expectedName, result.Message.SenderName);
        Assert.Equal(role, result.Message.SenderRole);
        Assert.True(result.Message.IsFromCenter);
        Assert.Equal("contacted", result.LeadStatus);
    }

    [Fact]
    public async Task GetAndSendLeadMessage_InvalidOrMissingLead_ThrowsExpectedExceptions()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Test", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        var service = new LeadService(leadRepo, centerRepo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetReceivedLeadMessagesForCenterUserAsync(centerOwnerId, "owner@center.com", UserRoles.Center, Guid.Empty));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SendLeadMessageForCenterUserAsync(centerOwnerId, "owner@center.com", UserRoles.Center, Guid.NewGuid(), "Xin chào"));
    }

    [Fact]
    public async Task GetAndSendLeadMessage_AsStudentRole_ThrowsUnauthorizedAccessException()
    {
        var studentId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = studentId, StudyCenterId = Guid.NewGuid(), Status = "new" });

        var service = new LeadService(leadRepo, centerRepo);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetReceivedLeadMessagesForCenterUserAsync(studentId, "student@example.com", UserRoles.Student, leadId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SendLeadMessageForCenterUserAsync(studentId, "student@example.com", UserRoles.Student, leadId, "Xin chào"));
    }
}
