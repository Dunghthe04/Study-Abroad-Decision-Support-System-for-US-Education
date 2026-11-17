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

        public Task<LeadMessage?> GetLeadMessageForUpdateAsync(Guid leadId, Guid messageId, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.Id == messageId && m.LeadId == leadId));

        public Task SaveLeadMessageAsync(LeadMessage message, CancellationToken ct = default) => Task.CompletedTask;

        public string? LastModerationFilter { get; private set; }

        public Task<int> CountRemovedCenterMessagesAsync(Guid centerId, CancellationToken ct = default) =>
            Task.FromResult(Messages.Count(m =>
                m.Status == "removed"
                && Leads.Any(l => l.Id == m.LeadId && l.StudyCenterId == centerId)
                && Users.TryGetValue(m.SenderUserId, out var u) && u.Role == UserRoles.Center));

        public Task<IReadOnlyList<RepeatRemovalCenterDto>> GetRepeatRemovalCentersAsync(int minRemoved, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepeatRemovalCenterDto>>([]);

        public Task<PagedResult<ModerationLeadMessageDto>> GetModerationMessagesAsync(
            string filter, int page, int pageSize, CancellationToken ct = default)
        {
            LastModerationFilter = filter;
            var rows = Messages
                .Where(m => filter switch
                {
                    "removed" => m.Status == "removed",
                    "flagged" => m.IsFlagged && m.Status != "removed",
                    "reported" => m.ReportedAt != null && m.Status != "removed",
                    "all" => m.Status == "removed" || m.IsFlagged || m.ReportedAt != null,
                    _ => (m.IsFlagged || m.ReportedAt != null) && m.Status != "removed",
                })
                .Select(m => new ModerationLeadMessageDto(
                    m.Id, m.LeadId, Guid.Empty, "Test Center", m.SenderUserId, UserRoles.Student, false,
                    m.Content, m.IsFlagged, m.FlagReason, m.Status, m.RemovedByUserId, m.CreatedAt))
                .ToList();
            return Task.FromResult(new PagedResult<ModerationLeadMessageDto>(rows, page, pageSize, rows.Count));
        }
    }

    private sealed class FakeProposalRepo : ICenterSuspensionProposalRepository
    {
        public List<CenterSuspensionProposal> Items { get; } = [];

        public Task<bool> HasPendingAsync(Guid centerId, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(p => p.StudyCenterId == centerId && p.Status == CenterSuspensionProposalStatuses.Pending));

        public Task AddAsync(CenterSuspensionProposal proposal, CancellationToken ct = default)
        {
            Items.Add(proposal);
            return Task.CompletedTask;
        }

        public Task<CenterSuspensionProposal?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

        public Task SaveAsync(CenterSuspensionProposal proposal, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PagedResult<CenterSuspensionProposalDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<CenterSuspensionProposalDto>([], page, pageSize, 0));
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
    public async Task GetModerationMessages_PendingIncludesReportedAndKeepClearsTheReport()
    {
        var (s, audit, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);
        var centerUser = AddCenterUser(s);
        var reported = new LeadMessage { LeadId = lead.Id, SenderUserId = centerUser.Id, Content = "Báo cáo", Status = "visible" };
        s.LeadRepo.Messages.Add(reported);
        await service.ReportSentLeadMessageAsync(student.Id, lead.Id, reported.Id, "Vi phạm");
        audit.Logs.Clear();

        var pending = await service.GetModerationMessagesAsync(null, 1, 20);
        Assert.Equal(reported.Id, Assert.Single(pending.Items).Id);

        await service.KeepLeadMessageAsync(Guid.NewGuid(), lead.Id, reported.Id);

        Assert.Null(reported.ReportedAt);
        Assert.Null(reported.ReportReason);
        Assert.Equal("lead_message.keep", Assert.Single(audit.Logs).Action);
        Assert.Empty((await service.GetModerationMessagesAsync(null, 1, 20)).Items);
    }

    private static (CenterSuspensionProposalService Service, CreateLeadSetup S, FakeProposalRepo Proposals, FakeAuditRepo Audit) ProposalSetup()
    {
        var s = new CreateLeadSetup();
        var proposals = new FakeProposalRepo();
        var audit = new FakeAuditRepo();
        return (new CenterSuspensionProposalService(s.LeadRepo, s.CenterRepo, proposals, audit), s, proposals, audit);
    }

    private static void AddRemovedCenterMessages(CreateLeadSetup s, int count)
    {
        var lead = new Lead { UserId = Guid.NewGuid(), StudyCenterId = s.CenterId };
        s.LeadRepo.Leads.Add(lead);
        var centerUserId = Guid.NewGuid();
        s.LeadRepo.Users[centerUserId] = ("Tư vấn viên", UserRoles.Center);
        for (var i = 0; i < count; i++)
            s.LeadRepo.Messages.Add(new LeadMessage { LeadId = lead.Id, SenderUserId = centerUserId, Content = "x", Status = "removed" });
    }

    [Fact]
    public async Task ProposeSuspension_BelowThreshold_IsRejected()
    {
        var (service, s, proposals, _) = ProposalSetup();
        AddRemovedCenterMessages(s, CenterSuspensionProposalService.MinRemovedMessages - 1);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ProposeAsync(Guid.NewGuid(), s.CenterId, "Gỡ nhiều lần"));
        Assert.Empty(proposals.Items);
    }

    [Fact]
    public async Task ProposeSuspension_AtThreshold_CreatesPendingProposalWithCountAndAudit()
    {
        var (service, s, proposals, audit) = ProposalSetup();
        AddRemovedCenterMessages(s, CenterSuspensionProposalService.MinRemovedMessages);
        var moderatorId = Guid.NewGuid();

        var dto = await service.ProposeAsync(moderatorId, s.CenterId, "  Chia sẻ số điện thoại nhiều lần  ");

        var saved = Assert.Single(proposals.Items);
        Assert.Equal("pending", saved.Status);
        Assert.Equal(CenterSuspensionProposalService.MinRemovedMessages, saved.RemovedMessageCount);
        Assert.Equal("Chia sẻ số điện thoại nhiều lần", saved.Reason);
        Assert.Equal(moderatorId, dto.ProposedByUserId);
        Assert.Equal("Trung Tâm Xác Minh", dto.StudyCenterName);
        Assert.Equal("center.suspension_proposed", Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task ProposeSuspension_DuplicatePendingSuspendedCenterOrEmptyReason_IsRejected()
    {
        var (service, s, _, _) = ProposalSetup();
        AddRemovedCenterMessages(s, 5);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ProposeAsync(Guid.NewGuid(), s.CenterId, "  "));
        await service.ProposeAsync(Guid.NewGuid(), s.CenterId, "Lần một");
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProposeAsync(Guid.NewGuid(), s.CenterId, "Lần hai"));

        s.CenterRepo.Centers[0].VerificationStatus = CenterVerificationStatuses.Suspended;
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProposeAsync(Guid.NewGuid(), s.CenterId, "Lần ba"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ProposeAsync(Guid.NewGuid(), Guid.NewGuid(), "Không có"));
    }

    [Fact]
    public async Task DecideSuspension_Approve_SuspendsCenterAndLogsAudit()
    {
        var (service, s, proposals, audit) = ProposalSetup();
        AddRemovedCenterMessages(s, 4);
        var proposal = await service.ProposeAsync(Guid.NewGuid(), s.CenterId, "Vi phạm lặp lại");
        audit.Logs.Clear();
        var adminId = Guid.NewGuid();

        var dto = await service.DecideAsync(adminId, proposal.Id, approve: true, note: "  Đồng ý  ");

        Assert.Equal("approved", dto.Status);
        Assert.Equal(CenterVerificationStatuses.Suspended, s.CenterRepo.Centers[0].VerificationStatus);
        Assert.Equal("Đồng ý", s.CenterRepo.Centers[0].VerificationNote);
        Assert.Equal(adminId, proposals.Items[0].DecidedByUserId);
        Assert.NotNull(proposals.Items[0].DecidedAt);
        Assert.Equal("center.suspended", Assert.Single(audit.Logs).Action);
    }

    [Fact]
    public async Task DecideSuspension_Reject_LeavesCenterUntouched()
    {
        var (service, s, proposals, audit) = ProposalSetup();
        AddRemovedCenterMessages(s, 4);
        var proposal = await service.ProposeAsync(Guid.NewGuid(), s.CenterId, "Vi phạm lặp lại");
        audit.Logs.Clear();

        var dto = await service.DecideAsync(Guid.NewGuid(), proposal.Id, approve: false, note: null);

        Assert.Equal("rejected", dto.Status);
        Assert.Equal(CenterReferral.Verified, s.CenterRepo.Centers[0].VerificationStatus);
        Assert.Equal("center.suspension_rejected", Assert.Single(audit.Logs).Action);
        Assert.Equal("rejected", proposals.Items[0].Status);
    }

    [Fact]
    public async Task DecideSuspension_AlreadyDecidedOrUnknown_IsRejected()
    {
        var (service, s, _, _) = ProposalSetup();
        AddRemovedCenterMessages(s, 4);
        var proposal = await service.ProposeAsync(Guid.NewGuid(), s.CenterId, "Vi phạm lặp lại");
        await service.DecideAsync(Guid.NewGuid(), proposal.Id, approve: false, note: null);

        await Assert.ThrowsAsync<ArgumentException>(() => service.DecideAsync(Guid.NewGuid(), proposal.Id, approve: true, note: null));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DecideAsync(Guid.NewGuid(), Guid.NewGuid(), approve: true, note: null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync("nonsense", 1, 20));
    }

    [Fact]
    public async Task GetModerationMessages_DefaultsToFlaggedAndKeepsOriginalContentOfRemoved()
    {
        var leadRepo = new FakeLeadRepo();
        var leadId = Guid.NewGuid();
        leadRepo.Messages.Add(new LeadMessage { LeadId = leadId, Content = "Gọi 0912345678", IsFlagged = true, Status = "visible" });
        leadRepo.Messages.Add(new LeadMessage { LeadId = leadId, Content = "Tin bình thường", IsFlagged = false, Status = "visible" });
        leadRepo.Messages.Add(new LeadMessage { LeadId = leadId, Content = "Nội dung gốc đã gỡ", IsFlagged = true, Status = "removed" });
        var service = new LeadService(leadRepo, new FakeStudyCenterRepo(), new FakeUserRepository(), new FakeParentLinkRepository());

        var flagged = await service.GetModerationMessagesAsync(null, 1, 20);
        var removed = await service.GetModerationMessagesAsync("REMOVED", 1, 20);

        Assert.Equal("removed", leadRepo.LastModerationFilter);
        Assert.Equal("Gọi 0912345678", Assert.Single(flagged.Items).Content);
        Assert.Equal("Nội dung gốc đã gỡ", Assert.Single(removed.Items).Content);
    }

    [Fact]
    public async Task GetModerationMessages_UnknownFilter_ThrowsArgumentException()
    {
        var service = new LeadService(new FakeLeadRepo(), new FakeStudyCenterRepo(), new FakeUserRepository(), new FakeParentLinkRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetModerationMessagesAsync("unknown", 1, 20));
    }

    [Fact]
    public async Task KeepLeadMessage_FlaggedMessage_ClearsFlagKeepsVisibleAndLogsAudit()
    {
        var moderatorId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var message = new LeadMessage { LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = "Link tài liệu", IsFlagged = true, FlagReason = "Chứa liên kết ngoài", Status = "visible" };
        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = Guid.NewGuid() });
        leadRepo.Messages.Add(message);
        var audit = new FakeAuditRepo();
        var service = new LeadService(leadRepo, new FakeStudyCenterRepo(), new FakeUserRepository(), new FakeParentLinkRepository(), audit);

        var dto = await service.KeepLeadMessageAsync(moderatorId, leadId, message.Id);

        Assert.False(message.IsFlagged);
        Assert.Equal("visible", message.Status);
        Assert.Equal("Link tài liệu", dto.Content);
        var log = Assert.Single(audit.Logs);
        Assert.Equal("lead_message.keep", log.Action);
        Assert.Equal(moderatorId, log.ActorUserId);
    }

    [Fact]
    public async Task KeepLeadMessage_NotFlagged_ChangesNothingAndDoesNotLog()
    {
        var leadId = Guid.NewGuid();
        var message = new LeadMessage { LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = "ok", IsFlagged = false, Status = "visible" };
        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = Guid.NewGuid() });
        leadRepo.Messages.Add(message);
        var audit = new FakeAuditRepo();
        var service = new LeadService(leadRepo, new FakeStudyCenterRepo(), new FakeUserRepository(), new FakeParentLinkRepository(), audit);

        await service.KeepLeadMessageAsync(Guid.NewGuid(), leadId, message.Id);

        Assert.Empty(audit.Logs);
    }

    [Fact]
    public async Task KeepLeadMessage_RemovedMessageOrWrongLead_IsRejected()
    {
        var leadId = Guid.NewGuid();
        var removed = new LeadMessage { LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = "x", IsFlagged = true, Status = "removed" };
        var otherLeadMessage = new LeadMessage { LeadId = Guid.NewGuid(), SenderUserId = Guid.NewGuid(), Content = "y", IsFlagged = true, Status = "visible" };
        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = Guid.NewGuid() });
        leadRepo.Messages.Add(removed);
        leadRepo.Messages.Add(otherLeadMessage);
        var service = new LeadService(leadRepo, new FakeStudyCenterRepo(), new FakeUserRepository(), new FakeParentLinkRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => service.KeepLeadMessageAsync(Guid.NewGuid(), leadId, removed.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.KeepLeadMessageAsync(Guid.NewGuid(), leadId, otherLeadMessage.Id));
        Assert.True(otherLeadMessage.IsFlagged);
    }

    [Fact]
    public async Task RemoveLeadMessage_FlaggedMessage_MarksRemovedMasksContentAndLogsAudit()
    {
        var moderatorId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var message = new LeadMessage
        {
            LeadId = leadId, SenderUserId = senderId, Content = "Gọi 0912345678 nhé",
            IsFlagged = true, FlagReason = "Chứa số điện thoại liên hệ", Status = "visible",
        };

        var centerRepo = new FakeStudyCenterRepo();
        centerRepo.Centers.Add(new StudyCenter { Id = centerId, Name = "Trung Tâm A" });
        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = "new" });
        leadRepo.Messages.Add(message);
        var userRepo = new FakeUserRepository();
        userRepo.Users.Add(new User { Id = senderId, FullName = "Phụ huynh A", Role = UserRoles.Parent });
        var audit = new FakeAuditRepo();
        var service = new LeadService(leadRepo, centerRepo, userRepo, new FakeParentLinkRepository(), audit);

        var dto = await service.RemoveLeadMessageAsync(moderatorId, leadId, message.Id, "  Chia sẻ số điện thoại  ");

        Assert.Equal("removed", message.Status);
        Assert.Equal(moderatorId, message.RemovedByUserId);
        Assert.Equal(LeadMessageMapper.RemovedContentPlaceholder, dto.Content);
        Assert.Null(dto.FlagReason);
        var log = Assert.Single(audit.Logs);
        Assert.Equal("lead_message.remove", log.Action);
        Assert.Equal(moderatorId, log.ActorUserId);
        Assert.Equal(message.Id.ToString(), log.EntityId);
        using var detail = System.Text.Json.JsonDocument.Parse(log.DetailJson!);
        Assert.Equal("Chia sẻ số điện thoại", detail.RootElement.GetProperty("reason").GetString());
        Assert.True(detail.RootElement.GetProperty("wasFlagged").GetBoolean());
    }

    [Fact]
    public async Task RemoveLeadMessage_AlreadyRemoved_DoesNotLogAgainAndKeepsFirstRemover()
    {
        var firstModeratorId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var message = new LeadMessage { LeadId = leadId, SenderUserId = Guid.NewGuid(), Content = "x", Status = "removed", RemovedByUserId = firstModeratorId };

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = Guid.NewGuid() });
        leadRepo.Messages.Add(message);
        var audit = new FakeAuditRepo();
        var service = new LeadService(leadRepo, new FakeStudyCenterRepo(), new FakeUserRepository(), new FakeParentLinkRepository(), audit);

        var dto = await service.RemoveLeadMessageAsync(Guid.NewGuid(), leadId, message.Id, null);

        Assert.Equal(firstModeratorId, message.RemovedByUserId);
        Assert.Empty(audit.Logs);
        Assert.Equal(LeadMessageMapper.RemovedContentPlaceholder, dto.Content);
    }

    [Fact]
    public async Task RemoveLeadMessage_MessageOfAnotherLead_ThrowsKeyNotFoundAndKeepsMessageVisible()
    {
        var leadId = Guid.NewGuid();
        var otherLeadId = Guid.NewGuid();
        var message = new LeadMessage { LeadId = otherLeadId, SenderUserId = Guid.NewGuid(), Content = "x", Status = "visible" };

        var leadRepo = new FakeLeadRepo();
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = Guid.NewGuid() });
        leadRepo.Leads.Add(new Lead { Id = otherLeadId, UserId = Guid.NewGuid(), StudyCenterId = Guid.NewGuid() });
        leadRepo.Messages.Add(message);
        var service = new LeadService(leadRepo, new FakeStudyCenterRepo(), new FakeUserRepository(), new FakeParentLinkRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.RemoveLeadMessageAsync(Guid.NewGuid(), leadId, message.Id, null));
        Assert.Equal("visible", message.Status);
    }

    [Fact]
    public async Task RemoveLeadMessage_InvalidIdsOrTooLongReason_ThrowsArgumentException()
    {
        var service = new LeadService(new FakeLeadRepo(), new FakeStudyCenterRepo(), new FakeUserRepository(), new FakeParentLinkRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RemoveLeadMessageAsync(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RemoveLeadMessageAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', LeadService.MaxRemoveReasonLength + 1)));
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
        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository(), auditRepo);

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
        var service = new LeadService(leadRepo, centerRepo, new FakeUserRepository(), new FakeParentLinkRepository(), auditRepo);

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
        Assert.Null(thread.Messages[1].FlagReason);
        Assert.Equal("Trung Tâm Bảo Mật", thread.Messages[2].SenderName);
        Assert.True(thread.Messages[2].IsFromCenter);
        Assert.Equal(0, thread.FlaggedCount);
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
        leadRepo.Leads.Add(new Lead { Id = leadId, UserId = Guid.NewGuid(), StudyCenterId = centerId, Status = "new" });

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
        Assert.False(result.Message.IsFlagged);
        Assert.Equal("new", result.LeadStatus); // người gửi nhắn không đổi trạng thái
        Assert.Equal("new", lead.Status);
        Assert.Equal("lead_message.send", Assert.Single(audit.Logs).Action);

        var thread = await service.GetSentLeadMessagesAsync(student.Id, lead.Id);
        Assert.Equal(2, thread.Messages.Count);
        Assert.Equal("Trung Tâm Xác Minh", thread.Messages[0].SenderName);
        Assert.True(thread.Messages[0].IsFromCenter);
    }

    [Fact]
    public async Task SendSentLeadMessage_WithPhone_FlagsAndLogsFlaggedAudit()
    {
        var (s, audit, service) = MessageSetup();
        var student = s.AddStudent(18);
        var lead = AddLead(s, student.Id, student.Id);

        var result = await service.SendSentLeadMessageAsync(student.Id, lead.Id, "Trung tâm gọi em số 0912345678 nhé");

        Assert.True(result.Message.IsFlagged);
        Assert.False(string.IsNullOrEmpty(result.Message.FlagReason));
        Assert.Equal("visible", result.Message.Status); // vẫn hiện, chỉ gắn cờ
        Assert.Equal("lead_message.send_flagged", Assert.Single(audit.Logs).Action);
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
        var child17Self = AddLead(s, child17.Id, child17.Id);

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
        Assert.Equal(0, thread.FlaggedCount);
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
}
