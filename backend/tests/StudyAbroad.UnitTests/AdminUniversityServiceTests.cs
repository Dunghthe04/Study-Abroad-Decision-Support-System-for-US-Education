using StudyAbroad.Application.Admin.Universities;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AdminUniversityServiceTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public async Task CreateUniversityAsync_CreatesSchoolWithMedia()
    {
        var repo = new FakeRepository();
        var service = new AdminUniversityService(repo, new FakeAuditLogs());
        var result = await service.CreateUniversityAsync(
            new CreateUniversityRequest(" new-school ", " New School ", State: "ca", LogoUrl: "https://res.cloudinary.com/test/image/upload/logo.png"), Actor);

        Assert.True(result.Success);
        Assert.Equal("NEW-SCHOOL", result.Value!.Code);
        Assert.Equal("New School", result.Value.Name);
        Assert.Equal("CA", result.Value.State);
        Assert.Equal("https://res.cloudinary.com/test/image/upload/logo.png", result.Value.LogoUrl);
        Assert.Equal(result.Value.Id, Assert.Single(repo.MediaItems).EntityId);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task CreateUniversityAsync_RejectsDuplicateCode()
    {
        var repo = new FakeRepository();
        var service = new AdminUniversityService(repo, new FakeAuditLogs());
        var result = await service.CreateUniversityAsync(new CreateUniversityRequest(repo.University.Code, "Another School"), Actor);

        Assert.False(result.Success);
        Assert.Contains("code", result.Errors.Keys);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task UpdateUniversityAsync_ClearsMediaWhenUrlsAreEmpty()
    {
        var repo = new FakeRepository();
        var university = repo.University;
        repo.MediaItems.Add(new Media { EntityType = "University", EntityId = university.Id, Type = "logo", Url = "https://res.cloudinary.com/test/image/upload/logo.png" });
        var service = new AdminUniversityService(repo, new FakeAuditLogs());
        var result = await service.UpdateUniversityAsync(university.Id,
            new UpdateUniversityRequest(university.Name, university.City, university.State, university.Website, university.Control,
                university.AcceptanceRate, university.InternationalStudentCount, university.IsIvy, LogoUrl: ""), Actor);

        Assert.True(result.Success);
        Assert.Null(result.Value!.LogoUrl);
        Assert.Empty(repo.MediaItems);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task SearchAsync_ClampsPageAndPageSize()
    {
        var repo = new FakeRepository();
        var service = new AdminUniversityService(repo, new FakeAuditLogs());

        await service.SearchAsync(new AdminUniversityQuery(Page: 0, PageSize: 500));

        Assert.Equal(1, repo.LastQuery!.Page);
        Assert.Equal(100, repo.LastQuery.PageSize);
    }

    [Fact]
    public async Task UpdateUniversityAsync_InvalidValues_ReturnsFieldErrorsAndDoesNotSave()
    {
        var repo = new FakeRepository();
        var audit = new FakeAuditLogs();
        var service = new AdminUniversityService(repo, audit);

        var result = await service.UpdateUniversityAsync(
            repo.University.Id, new UpdateUniversityRequest("  ", null, "California", null, "state", 1.5m, -1, false), Actor);

        Assert.False(result.Success);
        Assert.Contains("name", result.Errors.Keys);
        Assert.Contains("state", result.Errors.Keys);
        Assert.Contains("control", result.Errors.Keys);
        Assert.Contains("acceptanceRate", result.Errors.Keys);
        Assert.Contains("internationalStudentCount", result.Errors.Keys);
        Assert.Equal(0, repo.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task UpdateUniversityAsync_ChangedName_SavesAndWritesAuditWithOldAndNewValue()
    {
        var repo = new FakeRepository();
        var audit = new FakeAuditLogs();
        var service = new AdminUniversityService(repo, audit);
        var u = repo.University;

        var result = await service.UpdateUniversityAsync(
            u.Id, new UpdateUniversityRequest("New Name", u.City, "ma", u.Website, u.Control, u.AcceptanceRate, u.InternationalStudentCount, u.IsIvy), Actor);

        Assert.True(result.Success);
        Assert.Equal("New Name", u.Name);
        Assert.Equal("MA", u.State);
        Assert.Equal(1, repo.SaveCount);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal("university.update", entry.Action);
        Assert.Equal(Actor, entry.ActorUserId);
        Assert.Contains("Old Name", entry.DetailJson);
        Assert.Contains("New Name", entry.DetailJson);
    }

    [Fact]
    public async Task UpdateUniversityAsync_NothingChanged_DoesNotWriteAudit()
    {
        var repo = new FakeRepository();
        var audit = new FakeAuditLogs();
        var service = new AdminUniversityService(repo, audit);
        var u = repo.University;

        var result = await service.UpdateUniversityAsync(
            u.Id, new UpdateUniversityRequest(u.Name, u.City, u.State, u.Website, u.Control, u.AcceptanceRate, u.InternationalStudentCount, u.IsIvy), Actor);

        Assert.True(result.Success);
        Assert.Equal(0, repo.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task UpdateUniversityAsync_UnknownId_ReturnsNotFound()
    {
        var service = new AdminUniversityService(new FakeRepository(), new FakeAuditLogs());

        var result = await service.UpdateUniversityAsync(Guid.NewGuid(), new UpdateUniversityRequest("X", null, null, null, null, null, null, false), Actor);

        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task UpdateOfferingAsync_OnlyOneSatValue_ReturnsError()
    {
        var repo = new FakeRepository();
        var service = new AdminUniversityService(repo, new FakeAuditLogs());

        var request = OfferingRequest(repo) with { Sat25 = 1200, Sat75 = null };
        var result = await service.UpdateOfferingAsync(repo.University.Id, repo.Offering.Id, request, Actor);

        Assert.Contains("sat25", result.Errors.Keys);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task UpdateOfferingAsync_UnknownMajor_ReturnsError()
    {
        var repo = new FakeRepository();
        var service = new AdminUniversityService(repo, new FakeAuditLogs());

        var request = OfferingRequest(repo) with { Majors = [new UpdateOfferingMajor(Guid.NewGuid(), false)] };
        var result = await service.UpdateOfferingAsync(repo.University.Id, repo.Offering.Id, request, Actor);

        Assert.Contains("majors", result.Errors.Keys);
    }

    [Fact]
    public async Task UpdateOfferingAsync_ReplacesMajors_NewMajorIsVerifiedAndAudited()
    {
        var repo = new FakeRepository();
        var audit = new FakeAuditLogs();
        var service = new AdminUniversityService(repo, audit);

        // Bỏ Engineering, giữ Computer Science, thêm Business.
        var request = OfferingRequest(repo) with
        {
            Majors = [new UpdateOfferingMajor(repo.ComputerScience.Id, true), new UpdateOfferingMajor(repo.Business.Id, false)],
        };
        var result = await service.UpdateOfferingAsync(repo.University.Id, repo.Offering.Id, request, Actor);

        Assert.True(result.Success);
        Assert.Contains(repo.Removed, l => l.MajorId == repo.Engineering.Id);
        var added = Assert.Single(repo.Added);
        Assert.Equal(repo.Business.Id, added.MajorId);
        Assert.True(added.IsVerified);
        Assert.Equal("university_offering.update", Assert.Single(audit.Entries).Action);
    }

    [Fact]
    public async Task UpdateOfferingAsync_UnknownReliability_ReturnsError()
    {
        var repo = new FakeRepository();
        var service = new AdminUniversityService(repo, new FakeAuditLogs());

        var request = OfferingRequest(repo) with { SourceReliability = "maybe" };
        var result = await service.UpdateOfferingAsync(repo.University.Id, repo.Offering.Id, request, Actor);

        Assert.Contains("sourceReliability", result.Errors.Keys);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task UpdateOfferingAsync_ChangedReliability_RecordsEditorAndAudit()
    {
        var repo = new FakeRepository();
        var audit = new FakeAuditLogs();
        var service = new AdminUniversityService(repo, audit);

        var request = OfferingRequest(repo) with { SourceReliability = "Official" };
        var result = await service.UpdateOfferingAsync(repo.University.Id, repo.Offering.Id, request, Actor);

        Assert.True(result.Success);
        Assert.Equal("official", repo.Offering.SourceReliability);
        Assert.Equal(Actor, repo.Offering.LastEditedByUserId);
        Assert.Equal("Moderator A", result.Value!.LastEditedBy);
        Assert.Contains("sourceReliability", Assert.Single(audit.Entries).DetailJson);
    }

    [Fact]
    public async Task UpdateUniversityAsync_Changed_RecordsEditor()
    {
        var repo = new FakeRepository();
        var service = new AdminUniversityService(repo, new FakeAuditLogs());
        var u = repo.University;

        var result = await service.UpdateUniversityAsync(
            u.Id, new UpdateUniversityRequest(u.Name, "Boston", u.State, u.Website, u.Control, u.AcceptanceRate, u.InternationalStudentCount, u.IsIvy), Actor);

        Assert.Equal(Actor, u.LastEditedByUserId);
        Assert.Equal("Moderator A", result.Value!.LastEditedBy);
    }

    [Fact]
    public async Task SetVisibilityAsync_Hide_WritesAudit_SecondCallDoesNot()
    {
        var repo = new FakeRepository();
        var audit = new FakeAuditLogs();
        var service = new AdminUniversityService(repo, audit);

        var first = await service.SetVisibilityAsync(repo.University.Id, false, Actor);
        var second = await service.SetVisibilityAsync(repo.University.Id, false, Actor);

        Assert.False(first!.IsActive);
        Assert.False(second!.IsActive);
        Assert.Equal("university.hide", Assert.Single(audit.Entries).Action);
    }

    [Fact]
    public async Task SetVisibilityAsync_UnknownId_ReturnsNull()
    {
        var service = new AdminUniversityService(new FakeRepository(), new FakeAuditLogs());

        Assert.Null(await service.SetVisibilityAsync(Guid.NewGuid(), false, Actor));
    }

    private static UpdateOfferingRequest OfferingRequest(FakeRepository repo)
    {
        var o = repo.Offering;
        return new UpdateOfferingRequest(
            o.AcademicYear, o.TuitionUsd, o.LivingUsd, o.FeesUsd, o.OtherExpensesUsd, o.MinGpa4, o.MinIelts, o.MinToefl,
            o.MinDuolingo, o.AvgGpa4, o.Sat25, o.Sat75, o.Act25, o.Act75, o.SatPolicy, o.SourceUrl, o.RetrievedAt,
            o.SourceReliability, o.Notes,
            [new UpdateOfferingMajor(repo.ComputerScience.Id, true), new UpdateOfferingMajor(repo.Engineering.Id, true)]);
    }

    private sealed class FakeAuditLogs : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];

        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepository : IAdminUniversityRepository
    {
        public University University { get; private set; } = new()
        {
            Code = "166683", Name = "Old Name", City = "Cambridge", State = "MA", Control = "private", AcceptanceRate = 0.05m,
        };
        public UniversityOffering Offering { get; }
        public Major ComputerScience { get; } = new() { Name = "Computer and information sciences", CipCode = "11" };
        public Major Engineering { get; } = new() { Name = "Engineering", CipCode = "14" };
        public Major Business { get; } = new() { Name = "Business/marketing", CipCode = "52" };
        public List<UniversityOfferingMajor> Links { get; } = [];
        public List<UniversityOfferingMajor> Added { get; } = [];
        public List<UniversityOfferingMajor> Removed { get; } = [];
        public List<Media> MediaItems { get; } = [];
        public AdminUniversityQuery? LastQuery { get; private set; }
        public int SaveCount { get; private set; }

        public FakeRepository()
        {
            Offering = new UniversityOffering
            {
                UniversityId = University.Id, StudyLevel = "undergraduate", TuitionUsd = 60000m, Sat25 = 1500, Sat75 = 1570,
            };
            Links.Add(new UniversityOfferingMajor { UniversityOfferingId = Offering.Id, MajorId = ComputerScience.Id, IsStem = true, IsVerified = true });
            Links.Add(new UniversityOfferingMajor { UniversityOfferingId = Offering.Id, MajorId = Engineering.Id, IsStem = true, IsVerified = true });
        }

        public Task<PagedResult<AdminUniversityListItemDto>> SearchAsync(AdminUniversityQuery query, CancellationToken ct = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<AdminUniversityListItemDto>([], query.Page, query.PageSize, 0));
        }

        public Task<bool> ExistsCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(University.Code == code);

        public void AddUniversity(University university) => University = university;

        public Task<IReadOnlyList<Media>> GetUniversityMediaAsync(Guid universityId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Media>>(MediaItems.Where(m => m.EntityType == "University" && m.EntityId == universityId).ToList());

        public Task SaveUniversityMediaAsync(Guid universityId, string? logoUrl, string? coverUrl, CancellationToken ct = default)
        {
            SetMedia(universityId, "logo", logoUrl);
            SetMedia(universityId, "cover", coverUrl);
            return Task.CompletedTask;
        }

        private void SetMedia(Guid universityId, string type, string? url)
        {
            var existing = MediaItems.FirstOrDefault(m => m.EntityId == universityId && m.Type == type);
            if (existing is not null) MediaItems.Remove(existing);
            if (!string.IsNullOrWhiteSpace(url))
            {
                MediaItems.Add(new Media { EntityType = "University", EntityId = universityId, Type = type, Url = url });
            }
        }

        public Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default) =>
            Task.FromResult(universityId == University.Id ? University : null);

        public Task<IReadOnlyList<UniversityOffering>> ListOfferingsAsync(Guid universityId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UniversityOffering>>([Offering]);

        public Task<UniversityOffering?> GetOfferingAsync(Guid universityId, Guid offeringId, CancellationToken ct = default) =>
            Task.FromResult(universityId == University.Id && offeringId == Offering.Id ? Offering : null);

        public Task<IReadOnlyList<AdminOfferingMajorRow>> ListMajorRowsAsync(IReadOnlyList<Guid> offeringIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AdminOfferingMajorRow>>([]);

        public Task<List<UniversityOfferingMajor>> GetMajorLinksAsync(Guid offeringId, CancellationToken ct = default) =>
            Task.FromResult(Links.ToList());

        public Task<IReadOnlyList<Major>> ListMajorsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Major>>([ComputerScience, Engineering, Business]);

        public Task<string?> GetUserDisplayNameAsync(Guid? userId, CancellationToken ct = default) =>
            Task.FromResult(userId == Actor ? "Moderator A" : null);

        public void AddMajorLink(UniversityOfferingMajor link) => Added.Add(link);

        public void RemoveMajorLink(UniversityOfferingMajor link) => Removed.Add(link);

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
