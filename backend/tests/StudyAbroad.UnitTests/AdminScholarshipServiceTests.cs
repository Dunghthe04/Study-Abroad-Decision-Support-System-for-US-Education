using StudyAbroad.Application.Admin.Scholarships;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AdminScholarshipServiceTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid SchoolId = Guid.NewGuid();

    private static SaveScholarshipRequest Valid() => new(
        SchoolId, " Merit Award ", null, "Undergraduate", 20000m, "partial", 3.5m, 6.5m,
        new DateOnly(2027, 1, 15), "Top 10%", "https://example.edu/aid", new DateOnly(2026, 11, 1), "official");

    [Fact]
    public async Task CreateAsync_Valid_SavesOnceWithAudit()
    {
        var repo = new FakeRepository();
        var result = await new AdminScholarshipService(repo).CreateAsync(Valid(), Actor);

        Assert.True(result.Success);
        var saved = Assert.Single(repo.Items);
        Assert.Equal("Merit Award", saved.Name);
        Assert.Equal("undergraduate", saved.StudyLevel);
        Assert.Equal(Actor, saved.LastEditedByUserId);
        Assert.Equal("School A", result.Value!.UniversityName);
        var audit = Assert.Single(repo.Audits);
        Assert.Equal("scholarship.create", audit.Action);
        Assert.Contains("20000", audit.DetailJson);
        Assert.Contains("https://example.edu/aid", audit.DetailJson);
        Assert.Contains("2026-11-01", audit.DetailJson);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_NoSchoolAndNoProvider_ReturnsError()
    {
        var repo = new FakeRepository();
        var result = await new AdminScholarshipService(repo).CreateAsync(Valid() with { UniversityId = null }, Actor);

        Assert.Contains("provider", result.Errors.Keys);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ProviderOnly_IsAccepted()
    {
        var repo = new FakeRepository();
        var result = await new AdminScholarshipService(repo).CreateAsync(
            Valid() with { UniversityId = null, Provider = "Fulbright" }, Actor);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateAsync_MalformedValues_ReturnFieldErrors()
    {
        var repo = new FakeRepository();
        var request = Valid() with
        {
            UniversityId = Guid.NewGuid(),
            StudyLevel = "bachelor",
            AmountUsd = -1,
            CoverageType = "lots",
            MinGpa4 = 5,
            MinIelts = 10,
            SourceUrl = "ftp://x",
            RetrievedAt = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
            SourceReliability = "rumour",
        };
        var result = await new AdminScholarshipService(repo).CreateAsync(request, Actor);

        foreach (var key in new[] { "universityId", "studyLevel", "amountUsd", "coverageType", "minGpa4", "minIelts", "sourceUrl", "retrievedAt", "sourceReliability" })
            Assert.Contains(key, result.Errors.Keys);
        Assert.Empty(repo.Audits);
    }

    [Fact]
    public async Task CreateAsync_MissingSourceAndDate_ReturnsErrors()
    {
        var result = await new AdminScholarshipService(new FakeRepository())
            .CreateAsync(Valid() with { SourceUrl = " ", RetrievedAt = null }, Actor);

        Assert.Contains("sourceUrl", result.Errors.Keys);
        Assert.Contains("retrievedAt", result.Errors.Keys);
    }

    [Fact]
    public async Task UpdateAsync_Changed_WritesOldAndNewValues()
    {
        var repo = new FakeRepository();
        var service = new AdminScholarshipService(repo);
        var created = (await service.CreateAsync(Valid(), Guid.NewGuid())).Value!;

        var result = await service.UpdateAsync(created.Id, Valid() with { AmountUsd = 25000m }, Actor);

        Assert.True(result.Success);
        Assert.Equal("Moderator A", result.Value!.LastEditedBy);
        var entry = repo.Audits.Last();
        Assert.Equal("scholarship.update", entry.Action);
        Assert.Contains("amountUsd", entry.DetailJson);
        Assert.Contains("20000", entry.DetailJson);
        Assert.Contains("25000", entry.DetailJson);
    }

    [Fact]
    public async Task UpdateAsync_NothingChanged_DoesNotSaveOrAudit()
    {
        var repo = new FakeRepository();
        var service = new AdminScholarshipService(repo);
        var created = (await service.CreateAsync(Valid(), Actor)).Value!;

        await service.UpdateAsync(created.Id, Valid(), Actor);

        Assert.Equal(1, repo.SaveCount);
        Assert.Single(repo.Audits);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        var result = await new AdminScholarshipService(new FakeRepository()).UpdateAsync(Guid.NewGuid(), Valid(), Actor);

        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task SetVisibilityAsync_HideRequiresReason_AndAuditsIt()
    {
        var repo = new FakeRepository();
        var service = new AdminScholarshipService(repo);
        var created = (await service.CreateAsync(Valid(), Actor)).Value!;

        var missing = await service.SetVisibilityAsync(created.Id, false, null, Actor);
        var hidden = await service.SetVisibilityAsync(created.Id, false, "Hết hạn", Actor);

        Assert.Contains("reason", missing.Errors.Keys);
        Assert.False(hidden.Value!.IsActive);
        var entry = repo.Audits.Last();
        Assert.Equal("scholarship.hide", entry.Action);
        Assert.Contains("H\\u1EBFt h\\u1EA1n", entry.DetailJson);
    }

    [Fact]
    public async Task SetVisibilityAsync_AlreadyHidden_WithoutReason_IsNoOp()
    {
        var repo = new FakeRepository();
        var service = new AdminScholarshipService(repo);
        var created = (await service.CreateAsync(Valid(), Actor)).Value!;
        await service.SetVisibilityAsync(created.Id, false, "Hết hạn", Actor);
        var audits = repo.Audits.Count;

        var again = await service.SetVisibilityAsync(created.Id, false, null, Actor);

        Assert.True(again.Success);
        Assert.False(again.Value!.IsActive);
        Assert.Equal(audits, repo.Audits.Count);
    }

    [Fact]
    public async Task SearchAsync_ClampsPaging()
    {
        var repo = new FakeRepository();
        await new AdminScholarshipService(repo).SearchAsync(new AdminScholarshipQuery(Page: -3, PageSize: 1000));

        Assert.Equal(1, repo.LastQuery!.Page);
        Assert.Equal(100, repo.LastQuery.PageSize);
    }

    private sealed class FakeRepository : IAdminScholarshipRepository
    {
        public List<Scholarship> Items { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public int SaveCount { get; private set; }
        public AdminScholarshipQuery? LastQuery { get; private set; }

        public Task<PagedResult<AdminScholarshipDto>> SearchAsync(AdminScholarshipQuery query, CancellationToken ct = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<AdminScholarshipDto>([], query.Page, query.PageSize, 0));
        }

        public Task<Scholarship?> GetAsync(Guid scholarshipId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(s => s.Id == scholarshipId));

        public Task<string?> GetUniversityNameAsync(Guid? universityId, CancellationToken ct = default) =>
            Task.FromResult(universityId == SchoolId ? "School A" : null);

        public Task<string?> GetUserDisplayNameAsync(Guid? userId, CancellationToken ct = default) =>
            Task.FromResult(userId == Actor ? "Moderator A" : null);

        public void Add(Scholarship scholarship) => Items.Add(scholarship);

        public void AddAudit(AuditLog entry) => Audits.Add(entry);

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
