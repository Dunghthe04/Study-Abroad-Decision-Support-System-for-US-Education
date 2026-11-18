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

        public List<Consent> Consents { get; } = [];

        public Task<LeadDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Leads.Where(l => l.Id == id).Select(l => new LeadDto(
                l.Id, l.UserId, l.StudyCenterId, "Test Center", l.ConsentId, l.StudyLevel,
                l.ContactPhone, l.Message, l.SharedProfile, l.Status, l.CreatedAt)
            {
                SubmittedByUserId = l.SubmittedByUserId,
                SharedFields = l.SharedFields,
            }).FirstOrDefault());

        public Task<Lead> CreateLeadAsync(Lead lead, Consent? consent = null, CancellationToken ct = default)
        {
            if (consent != null)
            {
                Consents.Add(consent);
                lead.ConsentId = consent.Id;
            }
            Leads.Add(lead);
            return Task.FromResult(lead);
        }

        public Task<int> CountBySubmitterSinceAsync(Guid submitterId, DateTime sinceUtc, CancellationToken ct = default) =>
            Task.FromResult(Leads.Count(l => l.SubmittedByUserId == submitterId && l.CreatedAt >= sinceUtc));

        public Task<DateTime?> GetLastToCenterAsync(Guid submitterId, Guid centerId, DateTime sinceUtc, CancellationToken ct = default) =>
            Task.FromResult(Leads
                .Where(l => l.SubmittedByUserId == submitterId && l.StudyCenterId == centerId && l.CreatedAt >= sinceUtc
                    && l.Status != LeadStatuses.Cancelled && l.Status != LeadStatuses.Expired
                    && !(l.Status == LeadStatuses.PendingParent && l.ParentTokenExpiresAt <= DateTime.UtcNow))
                .Max(l => (DateTime?)l.CreatedAt));

        // [UC-116] Trả bản sao như AsNoTracking
        public Task<Lead?> GetByParentTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
            Task.FromResult(Leads.Where(l => l.ParentTokenHash == tokenHash).Select(CloneLead).FirstOrDefault());

        public Task<bool> TryCompleteParentConfirmAsync(Guid leadId, LeadParentDecision decision, Consent? consent, CancellationToken ct = default)
        {
            var lead = Leads.FirstOrDefault(l => l.Id == leadId);
            if (lead is null || lead.Status != LeadStatuses.PendingParent)
                return Task.FromResult(false);

            if (consent != null)
            {
                Consents.Add(consent);
                lead.ConsentId = consent.Id;
            }
            lead.Status = decision.Status;
            lead.ParentEmail = decision.ParentEmail;
            lead.ParentDecidedAt = decision.DecidedAt;
            lead.ParentDecisionIp = decision.DecisionIp;
            lead.UpdatedAt = decision.UpdatedAt;
            return Task.FromResult(true);
        }

        private static Lead CloneLead(Lead l) => new()
        {
            Id = l.Id, UserId = l.UserId, StudyCenterId = l.StudyCenterId, ConsentId = l.ConsentId, StudyLevel = l.StudyLevel,
            ContactPhone = l.ContactPhone, Message = l.Message, SharedProfile = l.SharedProfile, SubmittedByUserId = l.SubmittedByUserId,
            SharedFields = [.. l.SharedFields], AcknowledgedCenterContact = l.AcknowledgedCenterContact, Status = l.Status,
            ParentTokenHash = l.ParentTokenHash, ParentTokenExpiresAt = l.ParentTokenExpiresAt, ParentEmail = l.ParentEmail,
            ParentDecidedAt = l.ParentDecidedAt, ParentDecisionIp = l.ParentDecisionIp, CreatedAt = l.CreatedAt, UpdatedAt = l.UpdatedAt,
        };

        // [UC-44] Giống LeadRepository.GetSentLeadsAsync nhưng chạy trên danh sách trong bộ nhớ
        public Task<PagedResult<SentLeadDto>> GetSentLeadsAsync(
            IReadOnlyCollection<Guid> studentIds,
            Guid? submitterId,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var q = Leads
                .Where(l => studentIds.Contains(l.UserId) || (submitterId != null && l.SubmittedByUserId == submitterId))
                .OrderByDescending(l => l.CreatedAt)
                .ToList();

            var items = q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new SentLeadDto(
                    l.Id, l.StudyCenterId, "Test Center",
                    l.Status == LeadStatuses.PendingParent && l.ParentTokenExpiresAt <= DateTime.UtcNow ? LeadStatuses.Expired : l.Status,
                    l.CreatedAt, l.SharedFields,
                    l.StudyLevel, l.ContactPhone, l.Message, l.UserId,
                    Users.TryGetValue(l.UserId, out var u) ? u.FullName : "Student",
                    l.SubmittedByUserId != null && l.SubmittedByUserId != l.UserId,
                    Messages.Count(m => m.LeadId == l.Id && m.Status == "visible"
                                     && m.SenderUserId != l.UserId && m.SenderUserId != l.SubmittedByUserId)))
                .ToList();

            return Task.FromResult(new PagedResult<SentLeadDto>(items, page, pageSize, q.Count));
        }

        public Task<CenterLeadsSummaryDto> GetReceivedLeadsAsync(
            Guid? centerId,
            string? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var q = Leads.Where(l => !LeadStatuses.IsHiddenFromCenter(l.Status));
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
            var lead = Leads.FirstOrDefault(l => l.Id == leadId && !LeadStatuses.IsHiddenFromCenter(l.Status));
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

        public Task<LeadMessage?> GetLeadMessageForUpdateAsync(Guid leadId, Guid messageId, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.Id == messageId && m.LeadId == leadId));

        public Task SaveLeadMessageAsync(LeadMessage message, CancellationToken ct = default) => Task.CompletedTask;

        public List<Guid> AdminIds { get; } = [];
        public List<Notification> Notifications { get; } = [];

        public Task<PagedResult<AdminLeadMessageReportDto>> GetReportedMessagesAsync(
            string filter, int page, int pageSize, CancellationToken ct = default)
        {
            var rows = Messages
                .Where(m => m.ReportedAt != null && filter switch
                {
                    "resolved" => m.Status == "removed" || m.ReportResolution != null,
                    "all" => true,
                    _ => m.Status != "removed" && m.ReportResolution == null,
                })
                .Select(m => new AdminLeadMessageReportDto(
                    m.Id, m.LeadId, Guid.Empty, "Test Center", m.SenderUserId, "Người gửi", null, UserRoles.Student, "active", false,
                    m.Content, m.AttachmentUrl, m.Status, m.ReportedByUserId, m.ReportedAt, m.ReportReason, m.CreatedAt))
                .ToList();
            return Task.FromResult(new PagedResult<AdminLeadMessageReportDto>(rows, page, pageSize, rows.Count));
        }

        public Task<int> RemoveAllMessagesOfUserAsync(Guid senderUserId, Guid removedByUserId, CancellationToken ct = default)
        {
            var mine = Messages.Where(m => m.SenderUserId == senderUserId && m.Status != "removed").ToList();
            foreach (var m in mine) { m.Status = "removed"; m.RemovedByUserId = removedByUserId; }
            return Task.FromResult(mine.Count);
        }

        public int? ReportContextSizeSetting { get; set; }

        public Task<int?> GetReportContextSizeSettingAsync(CancellationToken ct = default) => Task.FromResult(ReportContextSizeSetting);

        public Task<IReadOnlyList<AdminReportContextMessageDto>> GetReportContextMessagesAsync(
            Guid leadId, Guid messageId, int contextSize, CancellationToken ct = default)
        {
            var thread = Messages.Where(m => m.LeadId == leadId).OrderBy(m => m.CreatedAt).ToList();
            var index = thread.FindIndex(m => m.Id == messageId);
            if (index < 0) return Task.FromResult<IReadOnlyList<AdminReportContextMessageDto>>([]);

            var from = Math.Max(index - contextSize, 0);
            var to = Math.Min(index + contextSize, thread.Count - 1);
            return Task.FromResult<IReadOnlyList<AdminReportContextMessageDto>>(thread
                .Skip(from).Take(to - from + 1)
                .Select(m => new AdminReportContextMessageDto(
                    m.Id, m.SenderUserId, "Người gửi", false, m.Content, m.AttachmentUrl, m.Status, m.CreatedAt, m.Id == messageId))
                .ToList());
        }

        public Task<IReadOnlyList<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(AdminIds.ToList());

        public Task AddNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
        {
            Notifications.AddRange(notifications);
            return Task.CompletedTask;
        }
    }

    private static User AddCenterUser(CreateLeadSetup s)
    {
        var user = new User { Id = Guid.NewGuid(), FullName = "Tư vấn viên", Role = UserRoles.Center };
        s.UserRepo.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task ReportSentLeadMessage_CenterMessage_StoresReporterAndLogsAudit()
    {
        var (s, audit, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var centerUser = AddCenterUser(s);
        var message = new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser.Id, Content = "Gọi 0912345678", Status = "visible" };
        s.LeadRepo.Messages.Add(message);

        var dto = await service.ReportSentLeadMessageAsync(student.Id, lead.Id, message.Id, "  Trung tâm xin số điện thoại  ");

        Assert.Equal(student.Id, message.ReportedByUserId);
        Assert.NotNull(message.ReportedAt);
        Assert.Equal("Trung tâm xin số điện thoại", message.ReportReason);
        Assert.Equal("visible", message.Status);
        Assert.True(dto.IsFromCenter);
        Assert.Equal("lead_message.report", Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task ReportSentLeadMessage_ReportedAgain_KeepsFirstReportAndDoesNotLogAgain()
    {
        var (s, audit, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var centerUser = AddCenterUser(s);
        var message = new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser.Id, Content = "x", Status = "visible" };
        s.LeadRepo.Messages.Add(message);

        await service.ReportSentLeadMessageAsync(student.Id, lead.Id, message.Id, "Lần một");
        var firstAt = message.ReportedAt;
        await service.ReportSentLeadMessageAsync(student.Id, lead.Id, message.Id, "Lần hai");

        Assert.Equal("Lần một", message.ReportReason);
        Assert.Equal(firstAt, message.ReportedAt);
        Assert.Single(audit.Logs);
    }

    [Fact]
    public async Task ReportSentLeadMessage_OwnMessageRemovedMessageOrUnknown_IsRejected()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var centerUser = AddCenterUser(s);
        var own = new LeadMessage { LeadId = lead.Id, SenderUserId = student.Id, Content = "em", Status = "visible" };
        var removed = new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser.Id, Content = "x", Status = "removed" };
        s.LeadRepo.Messages.Add(own);
        s.LeadRepo.Messages.Add(removed);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReportSentLeadMessageAsync(student.Id, lead.Id, own.Id, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReportSentLeadMessageAsync(student.Id, lead.Id, removed.Id, null));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReportSentLeadMessageAsync(student.Id, lead.Id, Guid.NewGuid(), null));
        Assert.Null(own.ReportedAt);
    }

    [Fact]
    public async Task ReportSentLeadMessage_LeadOfAnotherStudent_ThrowsKeyNotFound()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var other = s.AddStudent(18);
        var lead = AddLead(s, other.Id, other.Id);
        var centerUser = AddCenterUser(s);
        var message = new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser.Id, Content = "x", Status = "visible" };
        s.LeadRepo.Messages.Add(message);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReportSentLeadMessageAsync(student.Id, lead.Id, message.Id, null));
        Assert.Null(message.ReportedAt);
    }

    [Fact]
    public async Task ReportSentLeadMessage_NotifiesEveryActiveAdminOnlyOnFirstReport()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var centerUser = AddCenterUser(s);
        var message = new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser.Id, Content = "x", Status = "visible" };
        s.LeadRepo.Messages.Add(message);
        s.LeadRepo.AdminIds.AddRange([Guid.NewGuid(), Guid.NewGuid()]);

        await service.ReportSentLeadMessageAsync(student.Id, lead.Id, message.Id, "Lời lẽ thô tục");
        await service.ReportSentLeadMessageAsync(student.Id, lead.Id, message.Id, "Báo lại");

        Assert.Equal(2, s.LeadRepo.Notifications.Count);
        Assert.All(s.LeadRepo.Notifications, n =>
        {
            Assert.Equal("lead_message_report", n.Type);
            Assert.Equal("/admin/lead-message-reports", n.LinkUrl);
            Assert.Contains("Lời lẽ thô tục", n.Body);
        });
        Assert.Equal(s.LeadRepo.AdminIds.OrderBy(x => x), s.LeadRepo.Notifications.Select(n => n.UserId).OrderBy(x => x));
    }

    [Fact]
    public async Task ReportReceivedLeadMessage_CenterReportsStudentMessage_StoresReporterAndNotifiesAdmin()
    {
        var (s, audit, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var centerUser = AddCenterUser(s);
        s.CenterRepo.Centers.First(c => c.Id == s.CenterId).OwnerUserId = centerUser.Id;
        var message = new LeadMessage { LeadId = lead.Id, SenderUserId = student.Id, Content = "Quấy rối", Status = "visible" };
        var own = new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser.Id, Content = "Tin của trung tâm", Status = "visible" };
        s.LeadRepo.Messages.Add(message);
        s.LeadRepo.Messages.Add(own);
        s.LeadRepo.Users[student.Id] = ("Học sinh", UserRoles.Student);
        s.LeadRepo.AdminIds.Add(Guid.NewGuid());

        var dto = await service.ReportReceivedLeadMessageAsync(
            centerUser.Id, null, UserRoles.Center, lead.Id, message.Id, "  Nhắn tin quấy rối  ");

        Assert.Equal(centerUser.Id, message.ReportedByUserId);
        Assert.Equal("Nhắn tin quấy rối", message.ReportReason);
        Assert.False(dto.IsFromCenter);
        Assert.Single(s.LeadRepo.Notifications);
        Assert.Equal("lead_message.report", Assert.Single(audit.Logs).Action);

        // trung tâm không báo cáo được tin của chính trung tâm
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReportReceivedLeadMessageAsync(centerUser.Id, null, UserRoles.Center, lead.Id, own.Id, null));
    }

    [Fact]
    public async Task GetReportedMessages_DefaultsToPendingAndRejectsUnknownFilter()
    {
        var (s, _, service) = MessageSetup();
        var leadId = Guid.NewGuid();
        s.LeadRepo.Messages.Add(new LeadMessage { LeadId = leadId, Content = "chờ xử lý", ReportedAt = DateTime.UtcNow, Status = "visible" });
        s.LeadRepo.Messages.Add(new LeadMessage { LeadId = leadId, Content = "đã gỡ", ReportedAt = DateTime.UtcNow, Status = "removed" });
        s.LeadRepo.Messages.Add(new LeadMessage { LeadId = leadId, Content = "không báo cáo", Status = "visible" });

        var pending = await service.GetReportedMessagesAsync(null, 1, 20);
        var resolved = await service.GetReportedMessagesAsync("RESOLVED", 1, 20);

        Assert.Equal("chờ xử lý", Assert.Single(pending.Items).Content);
        Assert.Equal("đã gỡ", Assert.Single(resolved.Items).Content);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetReportedMessagesAsync("flagged", 1, 20));
    }

    [Fact]
    public async Task DismissReport_ClearsReportKeepsMessageVisibleAndLogsAudit()
    {
        var (s, audit, service) = MessageSetup();
        var adminId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var message = new LeadMessage
        {
            LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = "ok", Status = "visible",
            ReportedAt = DateTime.UtcNow, ReportedByUserId = Guid.NewGuid(), ReportReason = "nhầm",
        };
        var untouched = new LeadMessage { LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = "ok", Status = "visible" };
        s.LeadRepo.Messages.Add(message);
        s.LeadRepo.Messages.Add(untouched);

        await service.DismissReportAsync(adminId, leadId, message.Id);
        await service.DismissReportAsync(adminId, leadId, untouched.Id); // chưa bị báo cáo: không đổi gì, không log

        Assert.Null(message.ReportedAt);
        Assert.Null(message.ReportReason);
        Assert.Equal("visible", message.Status);
        var log = Assert.Single(audit.Logs);
        Assert.Equal("lead_message.report_dismiss", log.Action);
        Assert.Equal(adminId, log.ActorUserId);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DismissReportAsync(adminId, leadId, Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveAllMessagesOfLockedSender_RemovesOnlyThatUsersVisibleMessagesAndLogsAudit()
    {
        var (s, audit, service) = MessageSetup();
        var adminId = Guid.NewGuid();
        var sender = Guid.NewGuid();
        var other = Guid.NewGuid();
        var a = new LeadMessage { LeadId = Guid.NewGuid(), SenderUserId = sender, Content = "a", Status = "visible" };
        var b = new LeadMessage { LeadId = Guid.NewGuid(), SenderUserId = sender, Content = "b", Status = "visible" };
        var c = new LeadMessage { LeadId = Guid.NewGuid(), SenderUserId = other, Content = "c", Status = "visible" };
        s.LeadRepo.Messages.AddRange([a, b, c]);

        var removed = await service.RemoveAllMessagesOfLockedSenderAsync(adminId, sender, "Quấy rối");

        Assert.Equal(2, removed);
        Assert.Equal("removed", a.Status);
        Assert.Equal(adminId, b.RemovedByUserId);
        Assert.Equal("visible", c.Status);
        Assert.Equal("lead_message.remove_all_of_locked_user", Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task GetMessageSenderId_ReturnsSenderOrThrowsWhenUnknown()
    {
        var (s, _, service) = MessageSetup();
        var leadId = Guid.NewGuid();
        var message = new LeadMessage { LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = "x" };
        s.LeadRepo.Messages.Add(message);

        Assert.Equal(message.SenderUserId, await service.GetMessageSenderIdAsync(leadId, message.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetMessageSenderIdAsync(leadId, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMessageSenderIdAsync(Guid.Empty, message.Id));
    }

    [Fact]
    public async Task SendSentLeadMessage_AnyContentIsAcceptedWithoutFilteringOrFlagging()
    {
        var (s, audit, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        lead.Status = LeadStatuses.Accepted; // [UC-74] chỉ nhắn khi đã chấp nhận

        var result = await service.SendSentLeadMessageAsync(student.Id, lead.Id, "Gọi em số 0912345678, xem https://example.com nhé");

        Assert.Equal("Gọi em số 0912345678, xem https://example.com nhé", result.Message.Content);
        Assert.Equal("visible", result.Message.Status);
        Assert.Equal("lead_message.send", Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task SendSentLeadMessage_WithImage_SavesAttachmentAndAllowsEmptyText()
    {
        var (s, audit, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        lead.Status = LeadStatuses.Accepted; // [UC-74] chỉ nhắn khi đã chấp nhận

        var result = await service.SendSentLeadMessageAsync(
            student.Id, lead.Id, "  ",
            _ => Task.FromResult(new LeadMessageAttachment("https://res.cloudinary.com/x/image/upload/a.png", "usas/a")));

        Assert.Equal("https://res.cloudinary.com/x/image/upload/a.png", result.Message.AttachmentUrl);
        var saved = Assert.Single(s.LeadRepo.Messages);
        Assert.Equal("usas/a", saved.AttachmentPublicId);
        Assert.Equal(string.Empty, saved.Content);
        Assert.Contains("\"hasAttachment\":true", Assert.Single(audit.Logs).DetailJson);
    }

    [Fact]
    public async Task SendSentLeadMessage_EmptyTextWithoutImage_IsRejectedAndImageIsNotUploadedForClosedLeadOrNonOwner()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var other = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var uploads = 0;
        Task<LeadMessageAttachment> Upload(CancellationToken _) { uploads++; return Task.FromResult(new LeadMessageAttachment("https://x/y.png", "y")); }

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendSentLeadMessageAsync(student.Id, lead.Id, "  "));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SendSentLeadMessageAsync(other.Id, lead.Id, "", Upload));
        lead.Status = "closed";
        await Assert.ThrowsAsync<LeadRuleException>(() => service.SendSentLeadMessageAsync(student.Id, lead.Id, "", Upload));

        Assert.Equal(0, uploads); // quyền và trạng thái được kiểm tra trước khi tải ảnh lên nhà cung cấp
        Assert.Empty(s.LeadRepo.Messages);
    }

    // [UC-74] Chỉ nhắn được khi yêu cầu Đã chấp nhận; các trạng thái khác bị chặn ở cả hai phía, trước khi tải ảnh lên
    [Theory]
    [InlineData(LeadStatuses.New, "lead_not_accepted")]
    [InlineData(LeadStatuses.Rejected, "lead_rejected")]
    [InlineData(LeadStatuses.Closed, "lead_closed")]
    [InlineData(LeadStatuses.Withdrawn, "lead_withdrawn")]
    [InlineData(LeadStatuses.Cancelled, "lead_cancelled")]
    [InlineData(LeadStatuses.Expired, "lead_expired")]
    public async Task SendMessage_LeadNotAccepted_IsBlockedOnBothSidesWithoutUploading(string status, string expectedCode)
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        lead.Status = status;
        var centerUser = AddCenterUser(s);
        s.CenterRepo.Centers.First(c => c.Id == s.CenterId).OwnerUserId = centerUser.Id;
        var uploads = 0;
        Task<LeadMessageAttachment> Upload(CancellationToken _) { uploads++; return Task.FromResult(new LeadMessageAttachment("https://x/y.png", "y")); }

        var studentEx = await Assert.ThrowsAsync<LeadRuleException>(() =>
            service.SendSentLeadMessageAsync(student.Id, lead.Id, "Xin chào", Upload));
        Assert.Equal(expectedCode, studentEx.Code);

        Task Center() => service.SendLeadMessageForCenterUserAsync(centerUser.Id, null, UserRoles.Center, lead.Id, "Chào em", Upload);
        if (LeadStatuses.IsHiddenFromCenter(status))
            await Assert.ThrowsAsync<KeyNotFoundException>(Center); // [UC-116] trung tâm không thấy lead đã hủy / hết hạn chờ phụ huynh
        else
            Assert.Equal(expectedCode, (await Assert.ThrowsAsync<LeadRuleException>(Center)).Code);
        Assert.Equal(0, uploads);
        Assert.Empty(s.LeadRepo.Messages);
        Assert.Equal(status, lead.Status);
    }

    [Fact]
    public async Task SendMessage_AcceptedLeadOlderThan90Days_IsExpired()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id, daysAgo: 91);
        lead.Status = LeadStatuses.Accepted;

        var ex = await Assert.ThrowsAsync<LeadRuleException>(() => service.SendSentLeadMessageAsync(student.Id, lead.Id, "Xin chào"));

        Assert.Equal("lead_expired", ex.Code);
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

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

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
        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

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

    private static (LeadService Service, FakeLeadRepo Repo, FakeAuditRepo Audit, Guid OwnerId, Guid CenterId) DecisionSetup()
    {
        var ownerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Quyết Định", OwnerUserId = ownerId });
        var repo = new FakeLeadRepo();
        var audit = new FakeAuditRepo();
        return (new LeadService(repo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository(), audit), repo, audit, ownerId, centerId);
    }

    [Fact]
    public async Task AcceptReceivedLead_NewLead_BecomesAcceptedAndLogsAudit()
    {
        var (service, repo, audit, ownerId, centerId) = DecisionSetup();
        var lead = new Lead { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = "new" };
        repo.Leads.Add(lead);

        await service.AcceptReceivedLeadAsync(ownerId, null, UserRoles.Center, lead.Id);

        Assert.Equal(LeadStatuses.Accepted, lead.Status);
        Assert.True(LeadStatuses.IsAccepted(lead.Status));
        Assert.Equal("lead.accept", Assert.Single(audit.Logs).Action);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task RejectReceivedLead_ParentSubmitted_NotifiesParentAndStudent()
    {
        var (service, repo, _, ownerId, centerId) = DecisionSetup();
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var lead = new Lead { Id = Guid.NewGuid(), UserId = studentId, SubmittedByUserId = parentId, StudyCenterId = centerId, Status = "new" };
        repo.Leads.Add(lead);

        await service.RejectReceivedLeadAsync(ownerId, null, UserRoles.Center, lead.Id);

        Assert.Equal(LeadStatuses.Rejected, lead.Status);
        Assert.Equal(new[] { parentId, studentId }.OrderBy(x => x), repo.Notifications.Select(n => n.UserId).OrderBy(x => x));
        Assert.All(repo.Notifications, n =>
        {
            Assert.Equal("lead_rejected", n.Type);
            Assert.Contains("Trung Tâm Quyết Định", n.Body);
        });
    }

    [Fact]
    public async Task AcceptOrRejectReceivedLead_NotNewOtherCenterOrExpired_IsRejected()
    {
        var (service, repo, _, ownerId, centerId) = DecisionSetup();
        var accepted = new Lead { Id = Guid.NewGuid(), StudyCenterId = centerId, Status = "accepted" };
        var other = new Lead { Id = Guid.NewGuid(), StudyCenterId = Guid.NewGuid(), Status = "new" };
        var old = new Lead { Id = Guid.NewGuid(), StudyCenterId = centerId, Status = "new", CreatedAt = DateTime.UtcNow.AddDays(-91) };
        var withdrawn = new Lead { Id = Guid.NewGuid(), StudyCenterId = centerId, Status = LeadStatuses.Withdrawn };
        var pendingParent = new Lead { Id = Guid.NewGuid(), StudyCenterId = centerId, Status = LeadStatuses.PendingParent };
        repo.Leads.AddRange([accepted, other, old, withdrawn, pendingParent]);

        var notNew = await Assert.ThrowsAsync<LeadRuleException>(() => service.RejectReceivedLeadAsync(ownerId, null, UserRoles.Center, accepted.Id));
        Assert.Equal("lead_not_new", notNew.Code);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AcceptReceivedLeadAsync(ownerId, null, UserRoles.Center, other.Id));
        Assert.Equal("lead_expired", (await Assert.ThrowsAsync<LeadRuleException>(() =>
            service.AcceptReceivedLeadAsync(ownerId, null, UserRoles.Center, old.Id))).Code);
        Assert.Equal("lead_withdrawn", (await Assert.ThrowsAsync<LeadRuleException>(() =>
            service.AcceptReceivedLeadAsync(ownerId, null, UserRoles.Center, withdrawn.Id))).Code);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AcceptReceivedLeadAsync(ownerId, null, UserRoles.Center, pendingParent.Id));
        Assert.Equal("new", other.Status);
        Assert.Empty(repo.Notifications);
    }

    [Fact]
    public async Task UpdateReceivedLeadStatus_RejectedLeadOrRejectedTarget_IsRefused()
    {
        var (service, repo, _, ownerId, centerId) = DecisionSetup();
        var rejected = new Lead { Id = Guid.NewGuid(), StudyCenterId = centerId, Status = "rejected" };
        var fresh = new Lead { Id = Guid.NewGuid(), StudyCenterId = centerId, Status = "new" };
        repo.Leads.AddRange([rejected, fresh]);

        await Assert.ThrowsAsync<LeadRuleException>(() => service.UpdateReceivedLeadStatusAsync(ownerId, null, UserRoles.Center, rejected.Id, "new"));
        // từ chối phải qua thao tác riêng để người gửi nhận thông báo
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateReceivedLeadStatusAsync(ownerId, null, UserRoles.Center, fresh.Id, "rejected"));
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal("new", fresh.Status);
    }

    [Fact]
    public async Task CreateLead_NotifiesCenterOwner_UC110()
    {
        var s = new CreateLeadSetup();
        var ownerId = Guid.NewGuid();
        s.CenterRepo.Centers[0].OwnerUserId = ownerId;
        var notiRepo = new NotificationTests.FakeNotificationRepo();
        var service = new LeadService(s.LeadRepo, s.CenterRepo, s.UserRepo, s.LinkRepo,
            notifications: new StudyAbroad.Application.Notifications.NotificationService(notiRepo));
        var student = s.AddStudent(18);

        await service.CreateLeadAsync(student.Id, s.Request(ack: true));

        var n = Assert.Single(notiRepo.Items);
        Assert.Equal(ownerId, n.UserId);
        Assert.Equal("lead_new", n.Type);
        Assert.Equal("/center/leads", n.LinkUrl);
    }

    [Fact]
    public async Task UpdateReceivedLeadStatus_NotifiesSubmitterAndStudentOnlyWhenChanged_UC110()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm B", OwnerUserId = centerOwnerId });
        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, StudyCenterId = centerId, Status = "new", UserId = studentId, SubmittedByUserId = parentId });
        var notiRepo = new NotificationTests.FakeNotificationRepo();
        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository(),
            notifications: new StudyAbroad.Application.Notifications.NotificationService(notiRepo));

        await service.UpdateReceivedLeadStatusAsync(centerOwnerId, null, UserRoles.Center, leadId, "contacted");
        await service.UpdateReceivedLeadStatusAsync(centerOwnerId, null, UserRoles.Center, leadId, "contacted"); // không đổi: không báo

        Assert.Equal(2, notiRepo.Items.Count);
        Assert.Equal(new[] { parentId, studentId }.OrderBy(x => x), notiRepo.Items.Select(n => n.UserId).OrderBy(x => x));
        Assert.All(notiRepo.Items, n => Assert.Equal("lead_status_changed", n.Type));
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

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

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

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateReceivedLeadStatusAsync(
                centerOwnerId,
                "owner@center.com",
                UserRoles.Center,
                leadId,
                "hacker_status"));
    }

    [Fact]
    public async Task UpdateReceivedLeadStatus_AnotherCenterLead_ThrowsKeyNotFoundException()
    {
        var centerOwnerId = Guid.NewGuid();
        var myCenterId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = myCenterId, Name = "Trung Tâm Của Tôi", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, StudyCenterId = otherCenterId, Status = "new" });

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateReceivedLeadStatusAsync(
                centerOwnerId,
                "owner@center.com",
                UserRoles.Center,
                leadId,
                "contacted"));
    }

    [Fact]
    public async Task SendLeadMessage_ValidCleanReply_OnAcceptedLead_SavesMessageKeepsStatusAndLogsAudit()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Minh bạch", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = studentId, StudyCenterId = centerId, Status = LeadStatuses.Accepted });

        var auditRepo = new FakeAuditRepo();
        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository(), auditRepo);

        var res = await service.SendLeadMessageForCenterUserAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            leadId,
            "  Chào em, trung tâm đã nhận hồ sơ và sẽ tư vấn lộ trình phù hợp.  ");

        Assert.Equal(LeadStatuses.Accepted, res.LeadStatus);
        Assert.Equal(LeadStatuses.Accepted, leadRepo.Leads[0].Status); // [UC-74] không còn tự chuyển trạng thái
        Assert.Equal("visible", res.Message.Status);
        Assert.Equal("Trung Tâm Minh bạch", res.Message.SenderName);
        Assert.Equal("Chào em, trung tâm đã nhận hồ sơ và sẽ tư vấn lộ trình phù hợp.", res.Message.Content);
        Assert.Single(leadRepo.Messages);
        Assert.Single(auditRepo.Logs);
        Assert.Equal("lead_message.send", auditRepo.Logs[0].Action);
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

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

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

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());
        var tooLong = new string('a', LeadService.MaxReplyMessageLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendLeadMessageForCenterUserAsync(centerOwnerId, "owner@center.com", UserRoles.Center, leadId, tooLong));
    }

    [Fact]
    public async Task GetAndSendLeadMessage_AnotherCenterLead_ThrowsKeyNotFoundException()
    {
        var centerOwnerId = Guid.NewGuid();
        var myCenterId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = myCenterId, Name = "Trung Tâm A", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = otherCenterId, Status = "new" });

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetReceivedLeadMessagesForCenterUserAsync(centerOwnerId, "a@center.com", UserRoles.Center, leadId));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
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
            Status = "removed",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        });
        leadRepo.Messages.Add(new LeadMessage
        {
            LeadId = leadId,
            SenderUserId = centerOwnerId,
            Content = "Chào em, vui lòng xem lộ trình gợi ý nhé.",
            Status = "visible",
            CreatedAt = DateTime.UtcNow,
        });

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

        var thread = await service.GetReceivedLeadMessagesForCenterUserAsync(
            centerOwnerId,
            "owner@center.com",
            UserRoles.Center,
            leadId);

        Assert.Equal(3, thread.Messages.Count);
        Assert.Equal("Học sinh / Phụ huynh (Ẩn danh)", thread.Messages[0].SenderName);
        Assert.False(thread.Messages[0].IsFromCenter);
        Assert.Equal("Tin nhắn đã bị gỡ do vi phạm quy tắc.", thread.Messages[1].Content);
        Assert.Equal("Trung Tâm Bảo Mật", thread.Messages[2].SenderName);
        Assert.True(thread.Messages[2].IsFromCenter);
    }

    [Fact]
    public async Task GetReceivedLeadMessages_WhenContactShared_ShowsStudentAndParentRealNames()
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
            // [UC-42] Chỉ chia sẻ contact (SharedProfile = false) vẫn hiện tên thật
            SharedProfile = false,
            SharedFields = [LeadSharedFields.Contact],
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

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

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
    [InlineData("accepted")]
    public async Task SendLeadMessage_OnAcceptedOrLegacyContactedLead_PreservesExistingStatus(string initialStatus)
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Test", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = initialStatus });

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

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
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = LeadStatuses.Accepted });

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

        var result = await service.SendLeadMessageForCenterUserAsync(
            staffId,
            "staff@usas.vn",
            role,
            leadId,
            "Hệ thống hỗ trợ kiểm tra hồ sơ liên hệ.");

        Assert.Equal(expectedName, result.Message.SenderName);
        Assert.Equal(role, result.Message.SenderRole);
        Assert.True(result.Message.IsFromCenter);
        Assert.Equal(LeadStatuses.Accepted, result.LeadStatus);
    }

    [Fact]
    public async Task GetAndSendLeadMessage_InvalidOrMissingLead_ThrowsExpectedExceptions()
    {
        var centerOwnerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Test", OwnerUserId = centerOwnerId });

        var leadRepo = new FakeLeadRepo();
        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

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

        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetReceivedLeadMessagesForCenterUserAsync(studentId, "student@example.com", UserRoles.Student, leadId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SendLeadMessageForCenterUserAsync(studentId, "student@example.com", UserRoles.Student, leadId, "Xin chào"));
    }

    // ===== UC-42: gửi yêu cầu liên hệ =====

    /// <summary>Dữ liệu chung cho test CreateLead: một trung tâm đã xác minh và các fake rỗng.</summary>
    private sealed class CreateLeadSetup
    {
        public FakeLeadRepo LeadRepo { get; } = new();
        public FakeStudyCenterRepo CenterRepo { get; } = new();
        public FakeUserRepository UserRepo { get; } = new();
        public FakeParentLinkRepository LinkRepo { get; } = new();
        public Guid CenterId { get; } = Guid.NewGuid();
        public LeadService Service { get; }

        public CreateLeadSetup()
        {
            CenterRepo.Centers.Add(new StudyCenter
            {
                Id = CenterId, Name = "Trung Tâm Xác Minh", IsActive = true, VerificationStatus = CenterReferral.Verified,
            });
            Service = new LeadService(LeadRepo, CenterRepo, UserRepo, LinkRepo);
        }

        /// <summary>Thêm học sinh <paramref name="age"/> tuổi (sinh nhật đã qua 1 ngày, theo giờ Việt Nam).</summary>
        public User AddStudent(int age)
        {
            var today = AgePolicy.TodayInVietnam(DateTime.UtcNow);
            var user = new User { Role = UserRoles.Student, DateOfBirth = today.AddYears(-age).AddDays(-1) };
            UserRepo.Users.Add(user);
            return user;
        }

        public User AddParent(params User[] linkedChildren)
        {
            var parent = new User { Role = UserRoles.Parent };
            UserRepo.Users.Add(parent);
            foreach (var child in linkedChildren)
            {
                LinkRepo.Links.Add(new ParentStudentLink
                {
                    StudentUserId = child.Id, ParentUserId = parent.Id, Status = ParentLinkStatuses.Active,
                });
            }
            return parent;
        }

        public CreateLeadRequest Request(
            IReadOnlyList<string>? fields = null,
            Guid? studentUserId = null,
            bool ack = false,
            bool consent = true,
            Guid? centerId = null) =>
            new(centerId ?? CenterId,
                StudentUserId: studentUserId,
                SharedFields: fields ?? [LeadSharedFields.Contact],
                ConsentAccepted: consent,
                AcknowledgedCenterContact: ack);
    }

    private static async Task<LeadRuleException> AssertRule(Func<Task> act, string code, int status)
    {
        var ex = await Assert.ThrowsAsync<LeadRuleException>(act);
        Assert.Equal(code, ex.Code);
        Assert.Equal(status, ex.Status);
        return ex;
    }

    [Fact]
    public async Task CreateLead_CenterNotVerified_Returns400()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);
        s.CenterRepo.Centers[0].VerificationStatus = "unverified";

        await AssertRule(() => s.Service.CreateLeadAsync(student.Id, s.Request()), "center_not_verified", 400);
        Assert.Empty(s.LeadRepo.Leads);
    }

    [Fact]
    public async Task CreateLead_Student15_AgeRestricted()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(15);

        await AssertRule(() => s.Service.CreateLeadAsync(student.Id, s.Request(ack: true)), "age_restricted", 403);
    }

    [Fact]
    public async Task CreateLead_Student16WithoutAcknowledgement_Returns400()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(16);

        await AssertRule(() => s.Service.CreateLeadAsync(student.Id, s.Request(ack: false)), "acknowledgement_required", 400);
    }

    [Fact]
    public async Task CreateLead_Student16WithAcknowledgement_SavesLeadAndConsent()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(16);

        var dto = await s.Service.CreateLeadAsync(
            student.Id,
            s.Request(fields: [LeadSharedFields.Goals, "CONTACT", LeadSharedFields.Goals], ack: true),
            clientIp: "1.2.3.4");

        var lead = Assert.Single(s.LeadRepo.Leads);
        Assert.Equal(student.Id, lead.UserId);
        Assert.Equal(student.Id, lead.SubmittedByUserId);
        Assert.Equal([LeadSharedFields.Contact, LeadSharedFields.Goals], lead.SharedFields); // bỏ trùng, đúng thứ tự chuẩn
        Assert.True(lead.SharedProfile);
        Assert.True(lead.AcknowledgedCenterContact);

        var consent = Assert.Single(s.LeadRepo.Consents);
        Assert.Equal(student.Id, consent.UserId);
        Assert.Equal(AgeFeatures.ShareWithCenter, consent.Purpose);
        Assert.Null(consent.GrantedByUserId);
        Assert.Equal("1.2.3.4", consent.GrantedIp);
        Assert.Equal(consent.Id, dto.ConsentId);
    }

    [Fact]
    public async Task CreateLead_Student18WithoutAcknowledgement_Passes()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);

        await s.Service.CreateLeadAsync(student.Id, s.Request());

        var lead = Assert.Single(s.LeadRepo.Leads);
        Assert.False(lead.SharedProfile); // chỉ chia sẻ contact
    }

    [Fact]
    public async Task CreateLead_ParentWithoutStudent_Returns400()
    {
        var s = new CreateLeadSetup();
        var parent = s.AddParent(s.AddStudent(14));

        await AssertRule(() => s.Service.CreateLeadAsync(parent.Id, s.Request()), "student_required", 400);
    }

    [Fact]
    public async Task CreateLead_ParentWithUnlinkedStudent_Returns403()
    {
        var s = new CreateLeadSetup();
        var parent = s.AddParent(s.AddStudent(14));
        var stranger = s.AddStudent(17);

        await AssertRule(() => s.Service.CreateLeadAsync(parent.Id, s.Request(studentUserId: stranger.Id)), "not_linked", 403);
    }

    [Fact]
    public async Task CreateLead_ParentForLinkedChild14_Passes()
    {
        var s = new CreateLeadSetup();
        var child = s.AddStudent(14);
        var parent = s.AddParent(child);

        await s.Service.CreateLeadAsync(parent.Id, s.Request(studentUserId: child.Id));

        var lead = Assert.Single(s.LeadRepo.Leads);
        Assert.Equal(child.Id, lead.UserId);
        Assert.Equal(parent.Id, lead.SubmittedByUserId);
        Assert.False(lead.AcknowledgedCenterContact);
        var consent = Assert.Single(s.LeadRepo.Consents);
        Assert.Equal(child.Id, consent.UserId);
        Assert.Equal(parent.Id, consent.GrantedByUserId);
    }

    [Fact]
    public async Task CreateLead_MissingContact_Returns400()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);

        await AssertRule(() => s.Service.CreateLeadAsync(student.Id, s.Request(fields: [LeadSharedFields.Academic])), "contact_required", 400);
    }

    [Fact]
    public async Task CreateLead_UnknownField_Returns400()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);

        await AssertRule(() => s.Service.CreateLeadAsync(student.Id, s.Request(fields: [LeadSharedFields.Contact, "password"])), "invalid_shared_fields", 400);
    }

    [Fact]
    public async Task CreateLead_ConsentNotAccepted_Returns400()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);

        await AssertRule(() => s.Service.CreateLeadAsync(student.Id, s.Request(consent: false)), "consent_required", 400);
    }

    [Fact]
    public async Task CreateLead_SameCenterWithin7Days_Returns429WithRetryTime()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);
        var sentAt = DateTime.UtcNow.AddDays(-3);
        s.LeadRepo.Leads.Add(new Lead { UserId = student.Id, SubmittedByUserId = student.Id, StudyCenterId = s.CenterId, CreatedAt = sentAt });

        var ex = await AssertRule(() => s.Service.CreateLeadAsync(student.Id, s.Request()), "center_cooldown", 429);
        Assert.Equal(sentAt.AddDays(7), ex.RetryAfterUtc);
    }

    [Fact]
    public async Task CreateLead_SameCenterAfter7Days_Passes()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);
        s.LeadRepo.Leads.Add(new Lead { UserId = student.Id, SubmittedByUserId = student.Id, StudyCenterId = s.CenterId, CreatedAt = DateTime.UtcNow.AddDays(-8) });

        await s.Service.CreateLeadAsync(student.Id, s.Request());

        Assert.Equal(2, s.LeadRepo.Leads.Count);
    }

    [Fact]
    public async Task CreateLead_SixthLeadInOneDay_Returns429()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);
        for (var i = 0; i < LeadService.MaxLeadsPerDay; i++)
            s.LeadRepo.Leads.Add(new Lead { UserId = student.Id, SubmittedByUserId = student.Id, StudyCenterId = Guid.NewGuid() });

        await AssertRule(() => s.Service.CreateLeadAsync(student.Id, s.Request()), "daily_limit", 429);
    }

    [Fact]
    public async Task CreateLead_ParentLeadsCountTowardParentLimit()
    {
        var s = new CreateLeadSetup();
        var child = s.AddStudent(14);
        var parent = s.AddParent(child);
        for (var i = 0; i < LeadService.MaxLeadsPerDay; i++)
            s.LeadRepo.Leads.Add(new Lead { UserId = child.Id, SubmittedByUserId = parent.Id, StudyCenterId = Guid.NewGuid() });

        // Phụ huynh hết lượt trong ngày
        await AssertRule(() => s.Service.CreateLeadAsync(parent.Id, s.Request(studentUserId: child.Id)), "daily_limit", 429);

        // Lead phụ huynh gửi không tính vào lượt của học sinh 18 tuổi khác
        var adult = s.AddStudent(18);
        await s.Service.CreateLeadAsync(adult.Id, s.Request());
        Assert.Equal(LeadService.MaxLeadsPerDay + 1, s.LeadRepo.Leads.Count);
    }

    [Fact]
    public async Task GetLeadById_ParentWhoSubmitted_CanReadChildLead()
    {
        var s = new CreateLeadSetup();
        var child = s.AddStudent(14);
        var parent = s.AddParent(child);
        var stranger = s.AddParent();

        var created = await s.Service.CreateLeadAsync(parent.Id, s.Request(studentUserId: child.Id));

        Assert.NotNull(await s.Service.GetLeadByIdAsync(parent.Id, created.Id));
        Assert.NotNull(await s.Service.GetLeadByIdAsync(child.Id, created.Id));
        Assert.Null(await s.Service.GetLeadByIdAsync(stranger.Id, created.Id));
    }

    // ===== UC-44: xem yêu cầu liên hệ đã gửi =====

    private static Lead AddLead(CreateLeadSetup s, Guid studentId, Guid submitterId, int daysAgo = 0)
    {
        var lead = new Lead
        {
            UserId = studentId, SubmittedByUserId = submitterId, StudyCenterId = s.CenterId,
            CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
        };
        s.LeadRepo.Leads.Add(lead);
        return lead;
    }

    [Fact]
    public async Task GetSentLeads_Student18_SeesOwnLeadsAndParentLeadsForThem()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);
        var parent = s.AddParent(student);
        var other = s.AddStudent(18);
        var own = AddLead(s, student.Id, student.Id, daysAgo: 2);
        var byParent = AddLead(s, student.Id, parent.Id, daysAgo: 1);
        AddLead(s, other.Id, other.Id);

        var result = await s.Service.GetSentLeadsAsync(student.Id, 1, 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal([byParent.Id, own.Id], result.Items.Select(i => i.Id)); // mới nhất trước
        Assert.True(result.Items[0].SubmittedByParent);
        Assert.False(result.Items[1].SubmittedByParent);
    }

    [Fact]
    public async Task GetSentLeads_Student15_AgeRestricted()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(15);
        AddLead(s, student.Id, student.Id);

        await AssertRule(() => s.Service.GetSentLeadsAsync(student.Id, 1, 10), "age_restricted", 403);
    }

    [Fact]
    public async Task GetSentLeads_Parent_SeesOwnAndLinkedChildLeads_NotStrangers()
    {
        var s = new CreateLeadSetup();
        var child14 = s.AddStudent(14);
        var child17 = s.AddStudent(17);
        var parent = s.AddParent(child14, child17);
        var stranger = s.AddStudent(18);
        var forChild14 = AddLead(s, child14.Id, parent.Id);
        var child17Self = AddLead(s, child17.Id, child17.Id);
        AddLead(s, stranger.Id, stranger.Id);

        var result = await s.Service.GetSentLeadsAsync(parent.Id, 1, 10);

        Assert.Equal(
            new[] { forChild14.Id, child17Self.Id }.OrderBy(x => x),
            result.Items.Select(i => i.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task GetSentLeads_RevokedLink_HidesChildOwnLeads_KeepsParentSubmitted()
    {
        var s = new CreateLeadSetup();
        var child = s.AddStudent(17);
        var parent = s.AddParent(child);
        var byParent = AddLead(s, child.Id, parent.Id);
        AddLead(s, child.Id, child.Id);
        s.LinkRepo.Links[0].Status = ParentLinkStatuses.Revoked;

        var result = await s.Service.GetSentLeadsAsync(parent.Id, 1, 10);

        Assert.Equal(byParent.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task GetSentLeads_CountsOnlyVisibleCenterMessages()
    {
        var s = new CreateLeadSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var centerUser = Guid.NewGuid();
        s.LeadRepo.Messages.Add(new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser, Status = "visible" });
        s.LeadRepo.Messages.Add(new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser, Status = "removed" });
        s.LeadRepo.Messages.Add(new LeadMessage { LeadId = lead.Id, SenderUserId = student.Id, Status = "visible" });

        var result = await s.Service.GetSentLeadsAsync(student.Id, 1, 10);

        Assert.Equal(1, Assert.Single(result.Items).CenterMessageCount);
    }

    [Fact]
    public async Task GetSentLeads_CenterRole_NotAllowed()
    {
        var s = new CreateLeadSetup();
        var centerUser = new User { Role = UserRoles.Center };
        s.UserRepo.Users.Add(centerUser);

        await AssertRule(() => s.Service.GetSentLeadsAsync(centerUser.Id, 1, 10), "role_not_allowed", 403);
    }

    // ===== Nhắn tin phía người gửi: học sinh / phụ huynh đọc và gửi tin trong yêu cầu của mình =====

    /// <summary>Setup UC-44 có thêm audit log để kiểm tra tin gửi đi được ghi lại.</summary>
    // ---- [UC-58] Admin xử lý báo cáo: ngữ cảnh, gỡ riêng tin, cảnh cáo, thông báo người báo cáo, nhật ký truy cập

    private static (LeadMessage Reported, Guid LeadId, Guid ReporterId) AddReportedThread(CreateLeadSetup s, int total = 9, int reportedIndex = 4)
    {
        var leadId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(-1);
        LeadMessage? reported = null;
        for (var i = 0; i < total; i++)
        {
            var m = new LeadMessage
            {
                LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = $"tin {i}", Status = "visible",
                CreatedAt = start.AddMinutes(i),
            };
            if (i == reportedIndex)
            {
                m.ReportedAt = DateTime.UtcNow;
                m.ReportedByUserId = reporterId;
                m.ReportReason = "thô tục";
                reported = m;
            }
            s.LeadRepo.Messages.Add(m);
        }
        return (reported!, leadId, reporterId);
    }

    [Fact]
    public async Task GetReportContext_ReturnsOnlyNeighboursUsingDefaultSizeAndLogsAccess()
    {
        var (s, audit, service) = MessageSetup();
        var (reported, leadId, _) = AddReportedThread(s);
        var adminId = Guid.NewGuid();

        var context = await service.GetReportContextAsync(adminId, leadId, reported.Id);

        Assert.Equal(LeadService.DefaultReportContextSize, context.ContextSize);
        // 3 tin trước + tin bị báo cáo + 3 tin sau, không phải cả 9 tin của cuộc trò chuyện
        Assert.Equal(["tin 1", "tin 2", "tin 3", "tin 4", "tin 5", "tin 6", "tin 7"], context.Messages.Select(m => m.Content).ToArray());
        Assert.Equal("tin 4", Assert.Single(context.Messages, m => m.IsReported).Content);
        var log = Assert.Single(audit.Logs);
        Assert.Equal("lead_message.report_view", log.Action);
        Assert.Equal(adminId, log.ActorUserId);
        Assert.Equal(reported.Id.ToString(), log.EntityId);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(0, 1)]
    [InlineData(99, 9)] // vượt trần thì cắt về MaxReportContextSize, ở đây đủ lấy hết 9 tin
    [InlineData(-4, 1)]
    public async Task GetReportContext_UsesConfiguredSizeWithinBounds(int configured, int expectedCount)
    {
        var (s, _, service) = MessageSetup();
        var (reported, leadId, _) = AddReportedThread(s);
        s.LeadRepo.ReportContextSizeSetting = configured;

        var context = await service.GetReportContextAsync(Guid.NewGuid(), leadId, reported.Id);

        Assert.Equal(expectedCount, context.Messages.Count);
        Assert.InRange(context.ContextSize, 0, LeadService.MaxReportContextSize);
    }

    [Fact]
    public async Task GetReportContext_MessageWithoutReport_IsNotFoundAndNotLogged()
    {
        var (s, audit, service) = MessageSetup();
        var leadId = Guid.NewGuid();
        var plain = new LeadMessage { LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = "bình thường", Status = "visible" };
        s.LeadRepo.Messages.Add(plain);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetReportContextAsync(Guid.NewGuid(), leadId, plain.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetReportContextAsync(Guid.NewGuid(), leadId, Guid.NewGuid()));
        Assert.Empty(audit.Logs);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("5", 5)]
    [InlineData("{\"size\": 2}", 2)]
    [InlineData("{\"other\": 2}", null)]
    [InlineData("\"ba\"", null)]
    [InlineData("không phải json", null)]
    public void ParseReportContextSize_ReadsNumberOrSizeProperty(string? json, int? expected) =>
        Assert.Equal(expected, LeadService.ParseReportContextSize(json));

    [Fact]
    public async Task GetReportedMessages_WithViewer_LogsQueueAccess_AndWarnedReportsCountAsResolved()
    {
        var (s, audit, service) = MessageSetup();
        var leadId = Guid.NewGuid();
        s.LeadRepo.Messages.Add(new LeadMessage { LeadId = leadId, Content = "chờ", ReportedAt = DateTime.UtcNow, Status = "visible" });
        s.LeadRepo.Messages.Add(new LeadMessage
        {
            LeadId = leadId, Content = "đã cảnh cáo", ReportedAt = DateTime.UtcNow, Status = "visible",
            ReportResolution = LeadService.ReportResolutionWarned,
        });
        var adminId = Guid.NewGuid();

        var pending = await service.GetReportedMessagesAsync(null, 1, 20, viewerAdminId: adminId);
        var resolved = await service.GetReportedMessagesAsync("resolved", 1, 20);

        Assert.Equal("chờ", Assert.Single(pending.Items).Content);
        Assert.Equal("đã cảnh cáo", Assert.Single(resolved.Items).Content);
        var log = Assert.Single(audit.Logs); // lần gọi không có viewer không ghi nhật ký
        Assert.Equal("lead_message.report_queue_view", log.Action);
        Assert.Equal(adminId, log.ActorUserId);
    }

    [Fact]
    public async Task RemoveReportedMessage_RemovesOnlyThatMessageLogsAndNotifiesSenderAndReporter()
    {
        var (s, audit, service) = MessageSetup();
        var (reported, leadId, reporterId) = AddReportedThread(s);
        var adminId = Guid.NewGuid();

        await service.RemoveReportedMessageAsync(adminId, leadId, reported.Id, "  Ngôn từ thô tục  ");

        Assert.Equal("removed", reported.Status);
        Assert.Equal(adminId, reported.RemovedByUserId);
        Assert.Equal(LeadService.ReportResolutionRemoved, reported.ReportResolution);
        Assert.NotNull(reported.ReportResolvedAt);
        Assert.Equal("tin 4", reported.Content); // bản gốc vẫn giữ trong DB
        Assert.All(s.LeadRepo.Messages.Where(m => m.Id != reported.Id), m => Assert.Equal("visible", m.Status));

        var log = Assert.Single(audit.Logs);
        Assert.Equal("lead_message.remove", log.Action);
        using var detail = System.Text.Json.JsonDocument.Parse(log.DetailJson!);
        Assert.Equal("Ngôn từ thô tục", detail.RootElement.GetProperty("reason").GetString());

        Assert.Equal(2, s.LeadRepo.Notifications.Count);
        Assert.Contains(s.LeadRepo.Notifications, n => n.UserId == reported.SenderUserId && n.Type == "lead_message_removed");
        Assert.Contains(s.LeadRepo.Notifications, n => n.UserId == reporterId && n.Type == "lead_message_report_result");
    }

    [Fact]
    public async Task RemoveReportedMessage_AlreadyRemoved_DoesNothing_AndBlankReasonIsRejected()
    {
        var (s, audit, service) = MessageSetup();
        var (reported, leadId, _) = AddReportedThread(s);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RemoveReportedMessageAsync(Guid.NewGuid(), leadId, reported.Id, "  "));
        Assert.Equal("visible", reported.Status);

        await service.RemoveReportedMessageAsync(Guid.NewGuid(), leadId, reported.Id, "vi phạm");
        await service.RemoveReportedMessageAsync(Guid.NewGuid(), leadId, reported.Id, "vi phạm");

        Assert.Single(audit.Logs);
        Assert.Equal(2, s.LeadRepo.Notifications.Count);
    }

    [Fact]
    public async Task WarnReportedSender_KeepsMessageMarksResolvedLogsAndNotifiesBothSides()
    {
        var (s, audit, service) = MessageSetup();
        var (reported, leadId, reporterId) = AddReportedThread(s);

        await service.WarnReportedSenderAsync(Guid.NewGuid(), leadId, reported.Id, "Lời lẽ không phù hợp.");

        Assert.Equal("visible", reported.Status);
        Assert.Equal(LeadService.ReportResolutionWarned, reported.ReportResolution);
        Assert.Equal("lead_message.warn", Assert.Single(audit.Logs).Action);
        Assert.Contains(s.LeadRepo.Notifications, n => n.UserId == reported.SenderUserId && n.Type == "lead_message_warning");
        Assert.Contains(s.LeadRepo.Notifications, n => n.UserId == reporterId && n.Type == "lead_message_report_result");

        // Báo cáo đã xử lý thì không cảnh cáo lần hai
        await Assert.ThrowsAsync<ArgumentException>(() => service.WarnReportedSenderAsync(Guid.NewGuid(), leadId, reported.Id, "lần hai"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.WarnReportedSenderAsync(Guid.NewGuid(), leadId, reported.Id, null));
        Assert.Single(audit.Logs);
    }

    [Fact]
    public async Task DismissReport_NotifiesReporterOfTheOutcome()
    {
        var (s, _, service) = MessageSetup();
        var (reported, leadId, reporterId) = AddReportedThread(s);

        await service.DismissReportAsync(Guid.NewGuid(), leadId, reported.Id);

        var notification = Assert.Single(s.LeadRepo.Notifications);
        Assert.Equal(reporterId, notification.UserId);
        Assert.Equal("lead_message_report_result", notification.Type);
        Assert.Null(reported.ReportedAt);
    }

    [Fact]
    public async Task MarkReportResolvedByLock_MarksLockedAndNotifiesReporter()
    {
        var (s, _, service) = MessageSetup();
        var (reported, leadId, reporterId) = AddReportedThread(s);

        await service.MarkReportResolvedByLockAsync(Guid.NewGuid(), leadId, reported.Id);
        await service.MarkReportResolvedByLockAsync(Guid.NewGuid(), leadId, Guid.NewGuid()); // tin không tồn tại: bỏ qua

        Assert.Equal(LeadService.ReportResolutionLocked, reported.ReportResolution);
        Assert.Equal(reporterId, Assert.Single(s.LeadRepo.Notifications).UserId);
    }

    private static (CreateLeadSetup S, FakeAuditRepo Audit, LeadService Service) MessageSetup()
    {
        var s = new CreateLeadSetup();
        var audit = new FakeAuditRepo();
        return (s, audit, new LeadService(s.LeadRepo, s.CenterRepo, s.UserRepo, s.LinkRepo, audit));
    }

    [Fact]
    public async Task SentLeadMessages_Student18_ReadsThreadAndSends_WithoutChangingStatus()
    {
        var (s, audit, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        lead.Status = LeadStatuses.Accepted; // [UC-74] chỉ nhắn khi đã chấp nhận
        var centerUser = Guid.NewGuid();
        s.LeadRepo.Users[centerUser] = ("Tư vấn viên", UserRoles.Center);
        s.LeadRepo.Messages.Add(new LeadMessage
        {
            LeadId = lead.Id, SenderUserId = centerUser, Content = "Chào em", Status = "visible",
        });

        var result = await service.SendSentLeadMessageAsync(student.Id, lead.Id, "  Em cảm ơn trung tâm  ");

        Assert.Equal("Em cảm ơn trung tâm", result.Message.Content);
        Assert.Equal(student.Id, result.Message.SenderUserId);
        Assert.False(result.Message.IsFromCenter);
        Assert.Equal(LeadStatuses.Accepted, result.LeadStatus); // người gửi nhắn không đổi trạng thái
        Assert.Equal(LeadStatuses.Accepted, lead.Status);
        Assert.Equal("lead_message.send", Assert.Single(audit.Logs).Action);

        var thread = await service.GetSentLeadMessagesAsync(student.Id, lead.Id);
        Assert.Equal(2, thread.Messages.Count);
        Assert.Equal("Trung Tâm Xác Minh", thread.Messages[0].SenderName);
        Assert.True(thread.Messages[0].IsFromCenter);
    }

    [Fact]
    public async Task SentLeadMessages_Student15_AgeRestricted()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(15);
        var parent = s.AddParent(student);
        var lead = AddLead(s, student.Id, parent.Id);

        await AssertRule(() => service.GetSentLeadMessagesAsync(student.Id, lead.Id), "age_restricted", 403);
        await AssertRule(() => service.SendSentLeadMessageAsync(student.Id, lead.Id, "Xin chào"), "age_restricted", 403);
        Assert.Empty(s.LeadRepo.Messages);
    }

    [Fact]
    public async Task SentLeadMessages_Parent_CanUseSubmittedLeadAndLinkedChildOwnLead()
    {
        var (s, _, service) = MessageSetup();
        var child14 = s.AddStudent(14);
        var child17 = s.AddStudent(17);
        var parent = s.AddParent(child14, child17);
        var forChild14 = AddLead(s, child14.Id, parent.Id);
        forChild14.Status = LeadStatuses.Accepted; // [UC-74] chỉ nhắn khi đã chấp nhận
        var child17Self = AddLead(s, child17.Id, child17.Id);
        child17Self.Status = LeadStatuses.Accepted; // [UC-74] chỉ nhắn khi đã chấp nhận

        await service.SendSentLeadMessageAsync(parent.Id, forChild14.Id, "Phụ huynh hỏi lịch tư vấn");
        await service.SendSentLeadMessageAsync(parent.Id, child17Self.Id, "Phụ huynh hỏi thêm");

        Assert.Single((await service.GetSentLeadMessagesAsync(parent.Id, forChild14.Id)).Messages);
        Assert.Single((await service.GetSentLeadMessagesAsync(parent.Id, child17Self.Id)).Messages);
    }

    [Fact]
    public async Task SentLeadMessages_RevokedLink_KeepsParentSubmitted_HidesChildOwnLead()
    {
        var (s, _, service) = MessageSetup();
        var child = s.AddStudent(17);
        var parent = s.AddParent(child);
        var byParent = AddLead(s, child.Id, parent.Id);
        var childSelf = AddLead(s, child.Id, child.Id);
        s.LinkRepo.Links[0].Status = ParentLinkStatuses.Revoked;

        await service.GetSentLeadMessagesAsync(parent.Id, byParent.Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSentLeadMessagesAsync(parent.Id, childSelf.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SendSentLeadMessageAsync(parent.Id, childSelf.Id, "Xin chào"));
    }

    [Fact]
    public async Task SentLeadMessages_OtherStudentOrUnlinkedParent_NotFound()
    {
        var (s, _, service) = MessageSetup();
        var owner = s.AddStudent(18);
        var otherStudent = s.AddStudent(18);
        var unlinkedParent = s.AddParent();
        var lead = AddLead(s, owner.Id, owner.Id);

        // Báo "không tìm thấy" như lead không tồn tại, không lộ lead của người khác
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSentLeadMessagesAsync(otherStudent.Id, lead.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SendSentLeadMessageAsync(otherStudent.Id, lead.Id, "Xin chào"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSentLeadMessagesAsync(unlinkedParent.Id, lead.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSentLeadMessagesAsync(owner.Id, Guid.NewGuid()));
        Assert.Empty(s.LeadRepo.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendSentLeadMessage_EmptyContent_ThrowsArgumentException(string content)
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendSentLeadMessageAsync(student.Id, lead.Id, content));
    }

    [Fact]
    public async Task SendSentLeadMessage_ExceedsMaxLength_ThrowsArgumentException()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendSentLeadMessageAsync(student.Id, lead.Id, new string('a', LeadService.MaxReplyMessageLength + 1)));
    }

    [Fact]
    public async Task SendSentLeadMessage_ClosedLead_ReadOnly()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        lead.Status = "closed";

        await AssertRule(() => service.SendSentLeadMessageAsync(student.Id, lead.Id, "Xin chào"), "lead_closed", 400);
        Assert.Empty(s.LeadRepo.Messages);
        Assert.Equal("closed", (await service.GetSentLeadMessagesAsync(student.Id, lead.Id)).LeadStatus); // vẫn đọc được
    }

    [Fact]
    public async Task SendSentLeadMessage_WithdrawnLead_Blocked()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        lead.Status = LeadStatuses.Withdrawn;

        await AssertRule(() => service.SendSentLeadMessageAsync(student.Id, lead.Id, "Xin chào"), "lead_withdrawn", 400);
        Assert.Empty(s.LeadRepo.Messages);
    }

    // [UC-80] Trung tâm còn thấy dòng "Đã thu hồi" nhưng không đổi trạng thái, không đọc/gửi tin được
    private static (FakeLeadRepo Repo, LeadService Service, Guid OwnerId, Guid LeadId) WithdrawnCenterSetup()
    {
        var ownerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm Thu Hồi", OwnerUserId = ownerId });
        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = LeadStatuses.Withdrawn });
        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository(), new FakeAuditRepo());
        return (leadRepo, service, ownerId, leadId);
    }

    [Fact]
    public async Task UpdateReceivedLeadStatus_WithdrawnLead_Blocked()
    {
        var (repo, service, ownerId, leadId) = WithdrawnCenterSetup();

        await AssertRule(() => service.UpdateReceivedLeadStatusAsync(ownerId, "owner@center.com", UserRoles.Center, leadId, "contacted"),
            "lead_withdrawn", 400);
        Assert.Equal(LeadStatuses.Withdrawn, repo.Leads[0].Status);
    }

    [Fact]
    public async Task SendLeadMessageForCenter_WithdrawnLead_Blocked()
    {
        var (repo, service, ownerId, leadId) = WithdrawnCenterSetup();

        await AssertRule(() => service.SendLeadMessageForCenterUserAsync(ownerId, "owner@center.com", UserRoles.Center, leadId, "Xin chào"),
            "lead_withdrawn", 400);
        Assert.Empty(repo.Messages);
        Assert.Equal(LeadStatuses.Withdrawn, repo.Leads[0].Status);
    }

    [Fact]
    public async Task GetReceivedLeadMessagesForCenter_WithdrawnLead_ReturnsEmptyThread()
    {
        var (repo, service, ownerId, leadId) = WithdrawnCenterSetup();
        repo.Messages.Add(new LeadMessage { LeadId = leadId, SenderUserId = repo.Leads[0].UserId, Content = "Tin cũ", Status = "visible" });

        var thread = await service.GetReceivedLeadMessagesForCenterUserAsync(ownerId, "owner@center.com", UserRoles.Center, leadId);

        Assert.Equal(LeadStatuses.Withdrawn, thread.LeadStatus);
        Assert.Empty(thread.Messages);
    }

    [Fact]
    public async Task SentLeadMessages_CenterRole_NotAllowed()
    {
        var (s, _, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var centerUser = new User { Role = UserRoles.Center };
        s.UserRepo.Users.Add(centerUser);

        await AssertRule(() => service.GetSentLeadMessagesAsync(centerUser.Id, lead.Id), "role_not_allowed", 403);
    }

    // ===== [UC-116] Học sinh dưới 16 gửi yêu cầu liên hệ: phụ huynh xác nhận qua link mail =====

    private sealed class ParentConfirmSetup
    {
        public CreateLeadSetup S { get; } = new();
        public FakeEmailSender Emails { get; } = new();
        public LeadService Leads { get; }
        public LeadParentConfirmService Confirm { get; }
        public User Student { get; }
        public ParentStudentLink Mom { get; }
        public ParentStudentLink Dad { get; }

        public ParentConfirmSetup()
        {
            Leads = new LeadService(S.LeadRepo, S.CenterRepo, S.UserRepo, S.LinkRepo, null, Emails);
            Confirm = new LeadParentConfirmService(S.LeadRepo, S.LinkRepo, S.UserRepo, S.CenterRepo, Emails);
            Student = S.AddStudent(14);
            Student.Email = "kid@test.com";
            Student.FullName = "Con";
            Mom = AddConsentedParent("mom@test.com");
            Dad = AddConsentedParent("dad@test.com");
        }

        public ParentStudentLink AddConsentedParent(string email)
        {
            var consent = new Consent { UserId = Student.Id, Purpose = "process_student_data", Granted = true, PolicyVersion = "v1.0" };
            S.LinkRepo.Consents.Add(consent);
            var link = new ParentStudentLink { StudentUserId = Student.Id, ParentEmail = email, Status = ParentLinkStatuses.Active, ConsentId = consent.Id };
            S.LinkRepo.Links.Add(link);
            return link;
        }

        public Task<LeadDto> Send() => Leads.CreateLeadAsync(Student.Id, S.Request(fields: [LeadSharedFields.Contact, LeadSharedFields.Academic], ack: true));

        public string TokenFor(ParentStudentLink link) =>
            Emails.SentEmails.Last(e => e.Purpose == "lead_parent_confirm" && e.To == link.ParentEmail).Code;

        public Lead Stored => S.LeadRepo.Leads.Single();
    }

    [Fact]
    public async Task ParentConfirm_Student14WithConsent_PendingNoConsent_MailsEveryActiveParent()
    {
        var p = new ParentConfirmSetup();
        p.S.LinkRepo.Links.Add(new ParentStudentLink { StudentUserId = p.Student.Id, ParentEmail = "old@test.com", Status = ParentLinkStatuses.Revoked });

        await p.Send();

        Assert.Equal(LeadStatuses.PendingParent, p.Stored.Status);
        Assert.Null(p.Stored.ConsentId);
        Assert.Empty(p.S.LeadRepo.Consents);
        Assert.NotNull(p.Stored.ParentTokenHash);
        Assert.True(p.Stored.ParentTokenExpiresAt > DateTime.UtcNow.AddDays(6));
        var mails = p.Emails.SentEmails.Where(e => e.Purpose == "lead_parent_confirm").ToList();
        Assert.Equal(["dad@test.com", "mom@test.com"], mails.Select(m => m.To).Order());
        Assert.NotEqual(mails[0].Code, mails[1].Code);
        Assert.DoesNotContain(mails, m => m.Code.Contains(p.Stored.ParentTokenHash!));
    }

    [Fact]
    public async Task ParentConfirm_Student16_NoParentStep()
    {
        var p = new ParentConfirmSetup();
        var older = p.S.AddStudent(16);

        await p.Leads.CreateLeadAsync(older.Id, p.S.Request(ack: true));

        Assert.Equal(LeadStatuses.New, p.Stored.Status);
        Assert.Null(p.Stored.ParentTokenHash);
        Assert.Empty(p.Emails.SentEmails);
    }

    [Fact]
    public async Task ParentConfirm_Lookup_ShowsPendingInfo()
    {
        var p = new ParentConfirmSetup();
        await p.Send();

        var info = await p.Confirm.LookupAsync(p.TokenFor(p.Mom));

        Assert.Equal(LeadParentConfirmStates.Pending, info.State);
        Assert.Equal("Con", info.StudentName);
        Assert.Equal(14, info.StudentAge);
        Assert.Equal("Trung Tâm Xác Minh", info.CenterName);
        Assert.Equal("mom@test.com", info.ParentEmail);
        Assert.Equal([LeadSharedFields.Contact, LeadSharedFields.Academic], info.SharedFields);
        Assert.NotNull(info.ExpiresAt);
        Assert.False(info.DecidedByYou);
    }

    [Fact]
    public async Task ParentConfirm_Accept_LeadNew_ConsentByEmail_StudentMailed()
    {
        var p = new ParentConfirmSetup();
        await p.Send();

        var info = await p.Confirm.AcceptAsync(p.TokenFor(p.Mom), "1.2.3.4");

        Assert.Equal(LeadParentConfirmStates.Accepted, info.State);
        Assert.True(info.DecidedByYou);
        Assert.Equal(LeadStatuses.New, p.Stored.Status);
        Assert.Equal("mom@test.com", p.Stored.ParentEmail);
        Assert.Equal("1.2.3.4", p.Stored.ParentDecisionIp);
        var consent = Assert.Single(p.S.LeadRepo.Consents);
        Assert.Equal(p.Stored.ConsentId, consent.Id);
        Assert.Equal(AgeFeatures.ShareWithCenter, consent.Purpose);
        Assert.Equal("mom@test.com", consent.GrantedByEmail);
        Assert.Equal(p.Student.Id, consent.UserId);
        Assert.Contains(p.Emails.SentEmails, e => e.To == "kid@test.com" && e.Purpose == "lead_parent_decision" && e.Code == LeadStatuses.New);
    }

    [Fact]
    public async Task ParentConfirm_Reject_LeadCancelled_NoConsent_StudentMailed()
    {
        var p = new ParentConfirmSetup();
        await p.Send();

        var info = await p.Confirm.RejectAsync(p.TokenFor(p.Dad), null);

        Assert.Equal(LeadParentConfirmStates.Rejected, info.State);
        Assert.Equal(LeadStatuses.Cancelled, p.Stored.Status);
        Assert.Null(p.Stored.ConsentId);
        Assert.Empty(p.S.LeadRepo.Consents);
        Assert.Contains(p.Emails.SentEmails, e => e.To == "kid@test.com" && e.Code == LeadStatuses.Cancelled);
    }

    [Fact]
    public async Task ParentConfirm_SecondParent_SeesResult_CannotDecideAgain()
    {
        var p = new ParentConfirmSetup();
        await p.Send();
        await p.Confirm.AcceptAsync(p.TokenFor(p.Mom), null);

        var info = await p.Confirm.LookupAsync(p.TokenFor(p.Dad));
        Assert.Equal(LeadParentConfirmStates.Accepted, info.State);
        Assert.False(info.DecidedByYou);

        await AssertRule(() => p.Confirm.RejectAsync(p.TokenFor(p.Dad), null), "parent_confirm_answered", 409);
        await AssertRule(() => p.Confirm.AcceptAsync(p.TokenFor(p.Mom), null), "parent_confirm_answered", 409);
        Assert.Equal(LeadStatuses.New, p.Stored.Status);
        Assert.Single(p.S.LeadRepo.Consents);
    }

    [Fact]
    public async Task ParentConfirm_PastDeadline_Expired410_StudentMailedOnce()
    {
        var p = new ParentConfirmSetup();
        await p.Send();
        p.Stored.ParentTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        await AssertRule(() => p.Confirm.LookupAsync(p.TokenFor(p.Mom)), "parent_confirm_expired", 410);
        await AssertRule(() => p.Confirm.AcceptAsync(p.TokenFor(p.Dad), null), "parent_confirm_expired", 410);

        Assert.Equal(LeadStatuses.Expired, p.Stored.Status);
        Assert.Single(p.Emails.SentEmails, e => e.Purpose == "lead_parent_decision" && e.Code == LeadStatuses.Expired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("abc.not-a-guid")]
    public async Task ParentConfirm_BadToken_404(string? token)
    {
        var p = new ParentConfirmSetup();
        await p.Send();

        await AssertRule(() => p.Confirm.LookupAsync(token), "parent_confirm_invalid", 404);
    }

    [Fact]
    public async Task ParentConfirm_WrongSecretOrOtherStudentsLink_404()
    {
        var p = new ParentConfirmSetup();
        await p.Send();
        var token = p.TokenFor(p.Mom);
        var other = new ParentStudentLink { StudentUserId = Guid.NewGuid(), ParentEmail = "x@test.com", Status = ParentLinkStatuses.Active };
        p.S.LinkRepo.Links.Add(other);

        await AssertRule(() => p.Confirm.LookupAsync("x" + token), "parent_confirm_invalid", 404);
        await AssertRule(() => p.Confirm.LookupAsync(LeadParentConfirm.ComposeToken(token.Split('.')[0], other.Id)), "parent_confirm_invalid", 404);
    }

    [Fact]
    public async Task ParentConfirm_ParentRevokedAfterMail_CannotDecide()
    {
        var p = new ParentConfirmSetup();
        await p.Send();
        p.S.LinkRepo.Links.Single(l => l.Id == p.Mom.Id).Status = ParentLinkStatuses.Revoked;

        await AssertRule(() => p.Confirm.AcceptAsync(p.TokenFor(p.Mom), null), "parent_confirm_invalid", 404);
        Assert.Equal(LeadStatuses.PendingParent, p.Stored.Status);
    }

    [Fact]
    public async Task ParentConfirm_Student14WithoutConsent_StillAgeRestricted()
    {
        var p = new ParentConfirmSetup();
        foreach (var l in p.S.LinkRepo.Links) l.Status = ParentLinkStatuses.Revoked;

        await AssertRule(() => p.Send(), "age_restricted", 403);
        Assert.Empty(p.S.LeadRepo.Leads);
    }

    [Fact]
    public async Task ParentConfirm_CancelledOrExpired_DoesNotBlockResend()
    {
        var p = new ParentConfirmSetup();
        await p.Send();
        await p.Confirm.RejectAsync(p.TokenFor(p.Mom), null);

        await p.Send();   // phụ huynh từ chối: gửi lại ngay, không bị chờ 7 ngày
        var second = p.S.LeadRepo.Leads.Last();
        second.ParentTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        await p.Send();   // lead trước quá hạn (chưa ai mở link) cũng không tính
        Assert.Equal(3, p.S.LeadRepo.Leads.Count);

        await AssertRule(() => p.Send(), "center_cooldown", 429);   // lead còn chờ thì vẫn tính
    }

    [Fact]
    public async Task ParentConfirm_Student14WithConsent_SeesSentLeads_ExpiredShownAsExpired()
    {
        var p = new ParentConfirmSetup();
        await p.Send();
        p.Stored.ParentTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var page = await p.Leads.GetSentLeadsAsync(p.Student.Id, 1, 10);

        Assert.Equal(LeadStatuses.Expired, Assert.Single(page.Items).Status);
    }

    [Fact]
    public async Task ParentConfirm_PendingLead_StudentCannotMessage()
    {
        var p = new ParentConfirmSetup();
        var lead = await p.Send();

        await AssertRule(() => p.Leads.SendSentLeadMessageAsync(p.Student.Id, lead.Id, "Xin chào"), "lead_not_open", 400);
    }

    [Fact]
    public void ParentConfirm_Token_RoundTrip()
    {
        var linkId = Guid.NewGuid();
        var token = LeadParentConfirm.ComposeToken("secret", linkId);

        Assert.True(LeadParentConfirm.TryParseToken(token, out var hash, out var parsed));
        Assert.Equal(linkId, parsed);
        Assert.Equal(StudyAbroad.Application.ParentLinks.ParentInvite.HashToken("secret"), hash);
        Assert.False(LeadParentConfirm.TryParseToken("a.b.c", out _, out _));
        Assert.False(LeadParentConfirm.TryParseToken(new string('a', 129), out _, out _));
    }

    [Theory]
    [InlineData(LeadStatuses.PendingParent)]
    [InlineData(LeadStatuses.Cancelled)]
    [InlineData(LeadStatuses.Expired)]
    public async Task ParentConfirm_CenterCannotSeeOrTouchUnconfirmedLead(string status)
    {
        var ownerId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "TT", OwnerUserId = ownerId });
        var leadRepo = new FakeLeadRepo();
        var hidden = new Lead { StudyCenterId = centerId, Status = status };
        leadRepo.Leads.Add(hidden);
        leadRepo.Leads.Add(new Lead { StudyCenterId = centerId, Status = LeadStatuses.New });
        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository());

        var list = await service.GetReceivedLeadsForCenterUserAsync(ownerId, "o@c.com", UserRoles.Center, null, "all", null, 1, 10);
        Assert.Equal(1, list.TotalCount);
        Assert.DoesNotContain(list.Items, i => i.Id == hidden.Id);

        Assert.Null(await service.GetReceivedLeadDetailForCenterUserAsync(ownerId, "o@c.com", UserRoles.Center, hidden.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateReceivedLeadStatusAsync(ownerId, "o@c.com", UserRoles.Center, hidden.Id, "contacted"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetReceivedLeadMessagesForCenterUserAsync(ownerId, "o@c.com", UserRoles.Center, hidden.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SendLeadMessageForCenterUserAsync(ownerId, "o@c.com", UserRoles.Center, hidden.Id, "Chào em"));
        Assert.Equal(status, hidden.Status);
    }

    [Theory]
    [InlineData("Em chuyển cọc qua stk 1903657891234 Techcombank nhé", true)]
    [InlineData("So tai khoan VCB: 0011004567891 Nguyen Van A", true)]
    [InlineData("Ngan hang MB bank stk: 098765432100", true)]
    [InlineData("Chào em, trung tâm tư vấn chương trình du học Mỹ", false)]
    [InlineData("Học phí dự kiến khoảng 500.000.000 VND / năm", false)]
    public void LeadMessageContentFilter_FlagsBankAccountInformation(string content, bool expectedFlagged)
    {
        var result = LeadMessageContentFilter.Inspect(content);
        Assert.Equal(expectedFlagged, result.IsFlagged);
        if (expectedFlagged)
        {
            Assert.Contains("tài khoản ngân hàng", result.FlagReason);
        }
    }
}
