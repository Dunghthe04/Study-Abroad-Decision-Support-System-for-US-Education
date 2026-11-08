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

    private sealed class FakeLeadRepo : ILeadRepository
    {
        public List<Lead> Leads { get; } = [];

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
}
