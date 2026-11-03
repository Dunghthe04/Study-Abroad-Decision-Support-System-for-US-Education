using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class LeadServiceTests
{
    private sealed class FakeStudyCenterRepository : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];

        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));

        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<StudyCenter>(Centers, 1, 20, Centers.Count));

        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudyCenter>>(Centers.Where(c => c.IsActive).ToList());

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Centers.Any(c => c.Code == code));

        public Task AddAsync(StudyCenter center, CancellationToken ct = default)
        {
            Centers.Add(center);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        public List<Lead> Leads { get; } = [];
        public List<Consent> Consents { get; } = [];
        public LeadQuery? LastReceivedQuery { get; private set; }

        public Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Leads.FirstOrDefault(l => l.Id == id));

        public Task<LeadDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default)
        {
            var l = Leads.FirstOrDefault(x => x.Id == id);
            if (l is null) return Task.FromResult<LeadDto?>(null);
            return Task.FromResult<LeadDto?>(new LeadDto(
                l.Id,
                l.UserId,
                l.StudyCenterId,
                "Center Test",
                l.ConsentId,
                l.StudyLevel,
                l.ContactPhone,
                l.Message,
                l.SharedProfile,
                l.Status,
                l.CreatedAt));
        }

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

        public Task<PagedResult<LeadDto>> GetMyLeadsAsync(Guid userId, LeadQuery query, CancellationToken ct = default)
        {
            LastReceivedQuery = query;
            var list = Leads.Where(l => l.UserId == userId);
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                list = list.Where(l => string.Equals(l.Status, query.Status, StringComparison.OrdinalIgnoreCase));
            }

            var dtos = list.Select(l => new LeadDto(
                l.Id,
                l.UserId,
                l.StudyCenterId,
                "Center Test",
                l.ConsentId,
                l.StudyLevel,
                l.ContactPhone,
                l.Message,
                l.SharedProfile,
                l.Status,
                l.CreatedAt)).ToList();

            return Task.FromResult(new PagedResult<LeadDto>(dtos, query.Page, query.PageSize, dtos.Count));
        }
    }

    [Fact]
    public async Task GetMyLeadsAsync_ClampsPaginationBoundsAndTrimsStatus()
    {
        var leadRepo = new FakeLeadRepository();
        var centerRepo = new FakeStudyCenterRepository();
        var service = new LeadService(leadRepo, centerRepo);
        var userId = Guid.NewGuid();

        // Query với Page = -5 (phải thành 1), PageSize = 999 (phải thành 100), Status có khoảng trắng
        var query = new LeadQuery(Status: "  new  ", Page: -5, PageSize: 999);
        await service.GetMyLeadsAsync(userId, query);

        Assert.NotNull(leadRepo.LastReceivedQuery);
        Assert.Equal(1, leadRepo.LastReceivedQuery.Page);
        Assert.Equal(100, leadRepo.LastReceivedQuery.PageSize);
        Assert.Equal("new", leadRepo.LastReceivedQuery.Status);
    }
}
