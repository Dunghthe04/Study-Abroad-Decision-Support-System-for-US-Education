using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>1.5d: phụ huynh không có hồ sơ riêng; ghép con thật với con managed, chép rồi xóa mềm con managed.</summary>
public class ParentProfileShareServiceTests
{
    /// <summary>Hồ sơ trong bộ nhớ; CommitProfileFlowAsync ghi lại điểm/hoạt động/chứng chỉ để kiểm tra bản chép.</summary>
    private sealed class FakeProfiles : IProfileFlowRepository
    {
        public Dictionary<Guid, StudentProfile> ByUser { get; } = [];
        public Dictionary<Guid, List<TranscriptScore>> Scores { get; } = [];
        public Dictionary<Guid, List<ProfileCertification>> Certs { get; } = [];
        public Dictionary<Guid, List<ProfileActivity>> Activities { get; } = [];
        public Func<Guid, bool> IsParent { get; set; } = _ => false;

        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(ByUser.GetValueOrDefault(userId));
        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default) => Task.CompletedTask;

        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default)
        {
            if (!ByUser.TryGetValue(userId, out var p))
                ByUser[userId] = p = new StudentProfile { Id = Guid.NewGuid(), UserId = userId };
            return Task.FromResult(p);
        }

        public Task<IReadOnlyList<MajorOption>> GetMajorOptionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MajorOption>>([]);
        public Task<IReadOnlyList<UsStateOption>> GetUsStateOptionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UsStateOption>>([]);

        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities,
            IEnumerable<ProfileCertification> certifications, CancellationToken ct = default)
        {
            ByUser[profile.UserId] = profile;
            Scores[profile.Id] = scores.ToList();
            Activities[profile.Id] = activities.ToList();
            Certs[profile.Id] = certifications.ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TranscriptScore>>(Scores.GetValueOrDefault(profileId) ?? []);
        public Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProfileActivity>>(Activities.GetValueOrDefault(profileId) ?? []);
        public Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProfileCertification>>(Certs.GetValueOrDefault(profileId) ?? []);

        public Task<IReadOnlyList<Guid>> GetParentUserIdsWithProfileAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(ByUser.Keys.Where(IsParent).ToList());

        public Task DeleteProfileAsync(Guid userId, CancellationToken ct = default)
        {
            ByUser.Remove(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAcademic : IAcademicProfileService
    {
        public Task<AcademicProfileResult> GetAcademicProfileAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(AcademicProfileResult.Ok(new AcademicProfileResponse(
                Guid.NewGuid(), userId, StudyLevels.Undergraduate, null, null, null, null, GradeScales.Scale10, null, null,
                null, null, null, null, null, null, null, null, [], DateTime.UtcNow, null)));
    }

    /// <summary>Trường mục tiêu trong bộ nhớ; thêm trùng trường thì báo lỗi như service thật.</summary>
    private sealed class FakeTargetSchools : ITargetSchoolService
    {
        public List<TargetSchoolDto> Rows { get; } = [];

        public TargetSchoolDto Add(Guid userId, Guid universityId, string status = "considering", string? notes = null)
        {
            var row = new TargetSchoolDto(Guid.NewGuid(), userId, universityId, "U", "Uni", null, null, null, null, null,
                "match", status, 0, notes, null, null, DateTime.UtcNow, null);
            Rows.Add(row);
            return row;
        }

        public Task<AddTargetSchoolResult> AddTargetSchoolAsync(Guid userId, AddTargetSchoolRequest request, CancellationToken ct = default)
        {
            if (Rows.Any(r => r.UserId == userId && r.UniversityId == request.UniversityId))
                return Task.FromResult(AddTargetSchoolResult.Fail("Trường đã có trong danh sách."));
            return Task.FromResult(AddTargetSchoolResult.Ok(Add(userId, request.UniversityId!.Value, notes: request.Notes)));
        }

        public Task<TargetSchoolSummaryDto> GetTargetSchoolsAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default)
        {
            var items = Rows.Where(r => r.UserId == userId).ToList();
            return Task.FromResult(new TargetSchoolSummaryDto(items, items.Count, 0, 0, 0, 0, null));
        }

        public Task<TargetSchoolDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UpdateApplicationRoundResult> UpdateApplicationRoundAsync(Guid userId, Guid targetSchoolId, UpdateApplicationRoundRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RemoveTargetSchoolAsync(Guid userId, Guid targetSchoolId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RemoveTargetSchoolByUniversityAsync(Guid userId, string universityIdentifier, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TargetSchoolDto?> CheckTargetSchoolAsync(Guid userId, string? universityCode, Guid? universityId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeScenarios : IScenarioRepository
    {
        public List<WhatIfSimulation> Rows { get; } = [];

        public Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(Rows.Count(r => r.UserId == userId));
        // Mới nhất trước: thêm sau đứng trước
        public Task<List<WhatIfSimulation>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Rows.Where(r => r.UserId == userId).Reverse().ToList());
        public Task<WhatIfSimulation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));
        public Task AddAsync(WhatIfSimulation row, CancellationToken ct = default)
        {
            Rows.Add(row);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(WhatIfSimulation row, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(WhatIfSimulation row, CancellationToken ct = default)
        {
            Rows.Remove(row);
            return Task.CompletedTask;
        }
    }

    private readonly FakeUserRepository _users = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeProfiles _profiles = new();
    private readonly FakeTargetSchools _schools = new();
    private readonly FakeScenarios _scenarios = new();
    private readonly FakeAuditLogRepository _audit = new();

    public ParentProfileShareServiceTests()
    {
        _profiles.IsParent = id => _users.Users.Any(u => u.Id == id && u.Role == UserRoles.Parent);
    }

    private ManagedChildService Managed() => new(_users, _links, new FakeUnitOfWork(), _audit);

    private ParentProfileShareService CreateService() =>
        new(_links, _users, _profiles, new FakeAcademic(), Managed(), _schools, _scenarios, _audit);

    private User AddUser(string email, string role)
    {
        var user = new User { Email = email, FullName = "User " + email, Role = role, Status = UserStatuses.Active, PasswordHash = "x" };
        _users.Users.Add(user);
        return user;
    }

    private async Task<User> AddManaged(User parent, string name = "Bé An")
    {
        var created = await Managed().AddAsync(parent.Id, new AddManagedChildRequest(name, new DateOnly(2010, 5, 1)), CancellationToken.None);
        return _users.Users.Single(u => u.Id == created.Child!.StudentUserId);
    }

    private ParentStudentLink AddActiveLink(User student, User parent)
    {
        var link = new ParentStudentLink
        {
            StudentUserId = student.Id,
            ParentEmail = parent.Email,
            ParentUserId = parent.Id,
            Status = ParentLinkStatuses.Active,
        };
        _links.Links.Add(link);
        return link;
    }

    // Hồ sơ đã gửi qua luồng 6 bước, kèm 1 điểm, 1 chứng chỉ, 1 hoạt động
    private StudentProfile AddCommittedProfile(User owner, string major = "Computer Science")
    {
        var profile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            IntendedMajor = major,
            OverallGpa = 8.5m,
            BudgetTier = "tier_2",
            PreferredStates = ["CA"],
            FlowSubmittedAt = DateTimeOffset.UtcNow,
            TranscriptFileUrl = "https://files/parent.pdf",
        };
        _profiles.ByUser[owner.Id] = profile;
        _profiles.Scores[profile.Id] = [new TranscriptScore { Id = Guid.NewGuid(), StudentProfileId = profile.Id, Subject = "Toán", Score = 9m, TermName = "Lớp 11 - Kỳ 1" }];
        _profiles.Certs[profile.Id] = [new ProfileCertification { Id = Guid.NewGuid(), StudentProfileId = profile.Id, Category = "english", TestType = "ielts", OverallScore = 7m }];
        _profiles.Activities[profile.Id] = [new ProfileActivity { Id = Guid.NewGuid(), StudentProfileId = profile.Id, Kind = "activity", Title = "CLB Robotics" }];
        return profile;
    }

    private ParentStudentLink Stored(Guid id) => _links.Links.Single(l => l.Id == id);
    private User UserOf(Guid id) => _users.Users.Single(u => u.Id == id);

    // ───────────── Trang Tài khoản của phụ huynh ─────────────

    [Fact]
    public async Task Overview_NoManagedChild_DoesNotAskToMatch()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        AddActiveLink(kid, parent);

        var overview = (await CreateService().GetParentOverviewAsync(parent.Id)).Value!;

        var child = Assert.Single(overview.Children);
        Assert.Equal(kid.Id, child.StudentUserId);
        Assert.False(child.IsManaged);
        Assert.Empty(overview.PendingMatches);
    }

    [Fact]
    public async Task Overview_RealChildAndManaged_AsksForRealChildOnly()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        kid.DateOfBirth = new DateOnly(2005, 2, 3);
        var link = AddActiveLink(kid, parent);

        var overview = (await CreateService().GetParentOverviewAsync(parent.Id)).Value!;

        var managedRow = overview.Children.Single(c => c.StudentUserId == managed.Id);
        Assert.True(managedRow.IsManaged);
        Assert.Equal(new DateOnly(2010, 5, 1), managedRow.DateOfBirth);
        var pending = Assert.Single(overview.PendingMatches);
        Assert.Equal(link.Id, pending.LinkId);
        // Ngày sinh hai bên để frontend hiện và cảnh báo khi lệch
        Assert.Equal(new DateOnly(2005, 2, 3), pending.DateOfBirth);
    }

    // ───────────── Ghép con ─────────────

    [Fact]
    public async Task Match_ChildWithoutProfile_CopiesProfileSchoolsScenarios_ThenDeletesManaged()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        var source = AddCommittedProfile(managed);
        var uni = Guid.NewGuid();
        _schools.Add(managed.Id, uni, notes: "Ghi chú");
        _schools.Add(managed.Id, Guid.NewGuid(), status: TargetSchoolStatuses.ParentSuggested);
        _scenarios.Rows.Add(new WhatIfSimulation { UserId = managed.Id, Title = "Cũ" });
        _scenarios.Rows.Add(new WhatIfSimulation { UserId = managed.Id, Title = "Mới" });
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);

        var result = await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, managed.Id));

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ProfileTransferStatuses.Copied, result.Value!.Status);
        var copy = _profiles.ByUser[kid.Id];
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal("Computer Science", copy.IntendedMajor);
        Assert.Equal(["CA"], copy.PreferredStates);
        Assert.NotSame(source.PreferredStates, copy.PreferredStates);
        Assert.Null(copy.TranscriptFileUrl);
        var score = Assert.Single(_profiles.Scores[copy.Id]);
        Assert.NotEqual(_profiles.Scores[source.Id][0].Id, score.Id);
        Assert.Single(_profiles.Certs[copy.Id]);
        Assert.Single(_profiles.Activities[copy.Id]);
        // Dòng phụ huynh gợi ý không chép
        var school = Assert.Single(_schools.Rows, r => r.UserId == kid.Id);
        Assert.Equal(uni, school.UniversityId);
        Assert.Equal("Ghi chú", school.Notes);
        // Kịch bản giữ thứ tự: mới nhất vẫn đứng trước
        Assert.Equal(["Mới", "Cũ"], (await _scenarios.ListByUserAsync(kid.Id)).Select(s => s.Title));

        Assert.Equal(ProfileTransferStatuses.Copied, Stored(link.Id).ProfileTransferStatus);
        Assert.NotNull(UserOf(managed.Id).DeletedAt);
        Assert.Contains(_audit.Entries, e => e.Action == "parent.managed_child.transfer");
        Assert.Empty((await CreateService().GetParentOverviewAsync(parent.Id)).Value!.PendingMatches);
    }

    [Fact]
    public async Task Match_ChildHasProfile_OffersAndNeverOverwrites()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        AddCommittedProfile(managed);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var kidProfile = AddCommittedProfile(kid, major: "Biology");
        var link = AddActiveLink(kid, parent);

        var result = await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, managed.Id));

        Assert.Equal(ProfileTransferStatuses.Offered, result.Value!.Status);
        Assert.Same(kidProfile, _profiles.ByUser[kid.Id]);
        Assert.Equal(managed.Id, Stored(link.Id).SourceManagedUserId);
        Assert.Null(UserOf(managed.Id).DeletedAt);
        var offer = Assert.Single((await CreateService().GetStudentOffersAsync(kid.Id)).Value!);
        Assert.Equal(link.Id, offer.LinkId);
    }

    [Fact]
    public async Task Match_ManagedWithoutProfile_StillMovesSchools()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        _schools.Add(managed.Id, Guid.NewGuid());
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var kidProfile = AddCommittedProfile(kid, major: "Biology");
        var link = AddActiveLink(kid, parent);

        var result = await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, managed.Id));

        Assert.Equal(ProfileTransferStatuses.Copied, result.Value!.Status);
        Assert.Same(kidProfile, _profiles.ByUser[kid.Id]);
        Assert.Single(_schools.Rows, r => r.UserId == kid.Id);
        Assert.NotNull(UserOf(managed.Id).DeletedAt);
    }

    [Fact]
    public async Task Match_None_SkipsAndKeepsManaged()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        AddCommittedProfile(managed);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);

        var result = await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, null));

        Assert.Equal(ProfileTransferStatuses.Skipped, result.Value!.Status);
        Assert.False(_profiles.ByUser.ContainsKey(kid.Id));
        Assert.Null(UserOf(managed.Id).DeletedAt);
        Assert.Empty((await CreateService().GetParentOverviewAsync(parent.Id)).Value!.PendingMatches);
    }

    [Fact]
    public async Task Match_Twice_Conflict()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);
        await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, null));

        var again = await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, managed.Id));

        Assert.Equal(ParentLinkError.Conflict, again.Error);
        Assert.Null(UserOf(managed.Id).DeletedAt);
    }

    [Fact]
    public async Task Match_ManagedOfAnotherParent_NotFound()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var other = AddUser("other@test.com", UserRoles.Parent);
        var foreign = await AddManaged(other);
        AddCommittedProfile(foreign);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);

        var result = await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, foreign.Id));

        Assert.Equal(ParentLinkError.NotFound, result.Error);
        Assert.False(_profiles.ByUser.ContainsKey(kid.Id));
        Assert.Null(Stored(link.Id).ProfileTransferStatus);
    }

    [Fact]
    public async Task Match_ManagedLinkAsRealChild_NotFound()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed1 = await AddManaged(parent, "Bé An");
        var managed2 = await AddManaged(parent, "Bé Bình");
        var managedLink = _links.Links.Single(l => l.StudentUserId == managed1.Id);

        var result = await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(managedLink.Id, managed2.Id));

        Assert.Equal(ParentLinkError.NotFound, result.Error);
    }

    // Một con managed đang chờ con thật A chọn thì không ghép thêm cho con thật B
    [Fact]
    public async Task Match_ManagedAlreadyOffered_Busy()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        AddCommittedProfile(managed);
        var kidA = AddUser("a@test.com", UserRoles.Student);
        var kidB = AddUser("b@test.com", UserRoles.Student);
        AddCommittedProfile(kidA, "Biology");
        AddCommittedProfile(kidB, "Physics");
        var linkA = AddActiveLink(kidA, parent);
        var linkB = AddActiveLink(kidB, parent);
        await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(linkA.Id, managed.Id));

        var result = await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(linkB.Id, managed.Id));

        Assert.Equal(ParentLinkError.Conflict, result.Error);
        Assert.Null(Stored(linkB.Id).ProfileTransferStatus);
    }

    // ───────────── Con chọn ─────────────

    [Theory]
    [InlineData(true, "Computer Science", ProfileTransferStatuses.Copied)]
    [InlineData(false, "Biology", ProfileTransferStatuses.Declined)]
    public async Task DecideOffer_EitherWay_MovesSchoolsAndDeletesManaged(bool accept, string expectedMajor, string expectedStatus)
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        AddCommittedProfile(managed);
        _schools.Add(managed.Id, Guid.NewGuid());
        var kid = AddUser("kid@test.com", UserRoles.Student);
        AddCommittedProfile(kid, major: "Biology");
        var link = AddActiveLink(kid, parent);
        await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, managed.Id));

        var result = await CreateService().DecideOfferAsync(kid.Id, link.Id, accept);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(expectedMajor, _profiles.ByUser[kid.Id].IntendedMajor);
        Assert.Equal(expectedStatus, Stored(link.Id).ProfileTransferStatus);
        Assert.Null(Stored(link.Id).SourceManagedUserId);
        Assert.Single(_schools.Rows, r => r.UserId == kid.Id);
        Assert.NotNull(UserOf(managed.Id).DeletedAt);
        Assert.Empty((await CreateService().GetStudentOffersAsync(kid.Id)).Value!);
    }

    [Fact]
    public async Task DecideOffer_OtherStudentsLink_NotFound()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        AddCommittedProfile(managed);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var stranger = AddUser("other@test.com", UserRoles.Student);
        AddCommittedProfile(kid, major: "Biology");
        var link = AddActiveLink(kid, parent);
        await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, managed.Id));

        var result = await CreateService().DecideOfferAsync(stranger.Id, link.Id, accept: true);

        Assert.Equal(ParentLinkError.NotFound, result.Error);
        Assert.Equal("Biology", _profiles.ByUser[kid.Id].IntendedMajor);
        Assert.Null(UserOf(managed.Id).DeletedAt);
    }

    // Phụ huynh xóa con managed khi con thật chưa chọn: đề nghị biến mất, chọn thì báo hết hiệu lực
    [Fact]
    public async Task DecideOffer_ManagedDeletedMeanwhile_OfferGone()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var managed = await AddManaged(parent);
        AddCommittedProfile(managed);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        AddCommittedProfile(kid, major: "Biology");
        var link = AddActiveLink(kid, parent);
        await CreateService().MatchAsync(parent.Id, new MatchManagedChildRequest(link.Id, managed.Id));
        await Managed().DeleteAsync(parent.Id, managed.Id, CancellationToken.None);

        Assert.Empty((await CreateService().GetStudentOffersAsync(kid.Id)).Value!);
        var result = await CreateService().DecideOfferAsync(kid.Id, link.Id, accept: true);

        Assert.Equal(ParentLinkError.Conflict, result.Error);
        Assert.Equal("Biology", _profiles.ByUser[kid.Id].IntendedMajor);
    }

    // Bố và mẹ đều nhập cho cùng một con: người ghép trước chép vào con trống, người sau thành đề nghị
    [Fact]
    public async Task MomAndDad_FirstCopies_SecondBecomesOffer()
    {
        var mom = AddUser("mom@test.com", UserRoles.Parent);
        var dad = AddUser("dad@test.com", UserRoles.Parent);
        var momManaged = await AddManaged(mom);
        var dadManaged = await AddManaged(dad);
        AddCommittedProfile(momManaged, "Economics");
        AddCommittedProfile(dadManaged, "Medicine");
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var momLink = AddActiveLink(kid, mom);
        var dadLink = AddActiveLink(kid, dad);

        await CreateService().MatchAsync(mom.Id, new MatchManagedChildRequest(momLink.Id, momManaged.Id));
        var dadResult = await CreateService().MatchAsync(dad.Id, new MatchManagedChildRequest(dadLink.Id, dadManaged.Id));

        Assert.Equal("Economics", _profiles.ByUser[kid.Id].IntendedMajor);
        Assert.Equal(ProfileTransferStatuses.Copied, Stored(momLink.Id).ProfileTransferStatus);
        Assert.Equal(ProfileTransferStatuses.Offered, dadResult.Value!.Status);
        var offer = Assert.Single((await CreateService().GetStudentOffersAsync(kid.Id)).Value!);
        Assert.Equal(dadLink.Id, offer.LinkId);
    }

    // ───────────── Sau khi liên kết ─────────────

    [Fact]
    public async Task PrepareMatch_NoManagedChild_MarksSkipped()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);

        await CreateService().PrepareMatchAfterLinkAsync(parent.Id, [Stored(link.Id)]);

        Assert.Equal(ProfileTransferStatuses.Skipped, Stored(link.Id).ProfileTransferStatus);
    }

    [Fact]
    public async Task PrepareMatch_HasManagedChild_LeavesForParentToChoose()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        await AddManaged(parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);

        await CreateService().PrepareMatchAfterLinkAsync(parent.Id, [Stored(link.Id)]);

        Assert.Null(Stored(link.Id).ProfileTransferStatus);
    }

    // ───────────── Job chuyển hồ sơ cũ của phụ huynh ─────────────

    [Fact]
    public async Task Migrate_ParentWithProfile_MovesToHolder_AndRerunDoesNothing()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        AddCommittedProfile(parent, "Economics");
        var uni = Guid.NewGuid();
        _schools.Add(parent.Id, uni);

        var migrated = await CreateService().MigrateLegacyParentProfilesAsync();

        Assert.Equal(1, migrated);
        Assert.False(_profiles.ByUser.ContainsKey(parent.Id));
        var holder = Assert.Single(_users.Users, u => u.Status == UserStatuses.Managed);
        Assert.Equal(ParentProfileShareService.LegacyHolderName, holder.FullName);
        Assert.Null(holder.DateOfBirth);
        Assert.Equal("Economics", _profiles.ByUser[holder.Id].IntendedMajor);
        Assert.Contains(_schools.Rows, r => r.UserId == holder.Id && r.UniversityId == uni);
        Assert.Contains(_audit.Entries, e => e.Action == "parent.profile.migrate");

        Assert.Equal(0, await CreateService().MigrateLegacyParentProfilesAsync());
        Assert.Single(_users.Users, u => u.Status == UserStatuses.Managed);
    }

    // Đề nghị cũ (trước 1.5d) đang chờ con chọn thì trỏ sang con managed giữ hồ sơ, con vẫn thấy đề nghị
    [Fact]
    public async Task Migrate_OldPendingOffer_PointsToHolder()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        AddCommittedProfile(parent, "Economics");
        var kid = AddUser("kid@test.com", UserRoles.Student);
        AddCommittedProfile(kid, "Biology");
        var link = AddActiveLink(kid, parent);
        link.ProfileTransferStatus = ProfileTransferStatuses.Offered;

        await CreateService().MigrateLegacyParentProfilesAsync();

        var holder = _users.Users.Single(u => u.Status == UserStatuses.Managed);
        Assert.Equal(holder.Id, Stored(link.Id).SourceManagedUserId);
        var offer = Assert.Single((await CreateService().GetStudentOffersAsync(kid.Id)).Value!);
        Assert.Equal(link.Id, offer.LinkId);

        await CreateService().DecideOfferAsync(kid.Id, link.Id, accept: true);
        Assert.Equal("Economics", _profiles.ByUser[kid.Id].IntendedMajor);
        Assert.NotNull(UserOf(holder.Id).DeletedAt);
    }

    // Đã chuyển cho con rồi thì hồ sơ đã ở con: chỉ xóa bản của phụ huynh, không tạo con managed
    [Fact]
    public async Task Migrate_AlreadyCopiedToChild_OnlyDeletes()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        AddCommittedProfile(parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);
        link.ProfileTransferStatus = ProfileTransferStatuses.Copied;

        Assert.Equal(1, await CreateService().MigrateLegacyParentProfilesAsync());

        Assert.False(_profiles.ByUser.ContainsKey(parent.Id));
        Assert.DoesNotContain(_users.Users, u => u.Status == UserStatuses.Managed);
    }

    [Fact]
    public async Task Migrate_DeletedParent_Untouched()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        parent.DeletedAt = DateTime.UtcNow;
        AddCommittedProfile(parent);

        Assert.Equal(0, await CreateService().MigrateLegacyParentProfilesAsync());

        Assert.True(_profiles.ByUser.ContainsKey(parent.Id));
        Assert.DoesNotContain(_users.Users, u => u.Status == UserStatuses.Managed);
    }

    // Hồ sơ phụ huynh chỉ là nháp (chưa gửi) thì không có gì để giữ: xóa, không tạo con managed
    [Fact]
    public async Task Migrate_DraftOnly_DeletesWithoutHolder()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        _profiles.ByUser[parent.Id] = new StudentProfile { Id = Guid.NewGuid(), UserId = parent.Id, DraftFlowStateJson = "{}" };

        Assert.Equal(1, await CreateService().MigrateLegacyParentProfilesAsync());

        Assert.False(_profiles.ByUser.ContainsKey(parent.Id));
        Assert.DoesNotContain(_users.Users, u => u.Status == UserStatuses.Managed);
    }
}
