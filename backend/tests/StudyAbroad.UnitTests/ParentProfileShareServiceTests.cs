using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>Quy tắc hồ sơ phụ huynh – con (Việt chốt 17/11/2026).</summary>
public class ParentProfileShareServiceTests
{
    /// <summary>Hồ sơ trong bộ nhớ; CommitProfileFlowAsync ghi lại điểm/hoạt động/chứng chỉ để kiểm tra bản chép.</summary>
    private sealed class FakeProfiles : IProfileFlowRepository
    {
        public Dictionary<Guid, StudentProfile> ByUser { get; } = [];
        public Dictionary<Guid, List<TranscriptScore>> Scores { get; } = [];
        public Dictionary<Guid, List<ProfileCertification>> Certs { get; } = [];
        public Dictionary<Guid, List<ProfileActivity>> Activities { get; } = [];

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
    }

    private sealed class FakeAcademic : IAcademicProfileService
    {
        public Task<AcademicProfileResult> GetAcademicProfileAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(AcademicProfileResult.Ok(new AcademicProfileResponse(
                Guid.NewGuid(), userId, StudyLevels.Undergraduate, null, null, null, null, GradeScales.Scale10, null, null,
                null, null, null, null, null, null, null, null, [], DateTime.UtcNow, null)));
    }

    private readonly FakeUserRepository _users = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeProfiles _profiles = new();

    private ParentProfileShareService CreateService() => new(_links, _users, _profiles, new FakeAcademic());

    private User AddUser(string email, string role)
    {
        var user = new User { Email = email, FullName = "User " + email, Role = role, Status = UserStatuses.Active, PasswordHash = "x" };
        _users.Users.Add(user);
        return user;
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

    [Fact]
    public async Task AutoShare_OneChildWithoutProfile_CopiesParentProfile()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);
        var source = AddCommittedProfile(parent);

        await CreateService().AutoShareAfterLinkAsync(parent.Id, []);

        var copy = _profiles.ByUser[kid.Id];
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal("Computer Science", copy.IntendedMajor);
        Assert.Equal(8.5m, copy.OverallGpa);
        Assert.Equal(["CA"], copy.PreferredStates);
        Assert.NotSame(source.PreferredStates, copy.PreferredStates);
        Assert.Null(copy.TranscriptFileUrl);
        Assert.NotNull(copy.FlowSubmittedAt);
        // Bản ghi con là bản mới, không dùng chung Id với phụ huynh
        var score = Assert.Single(_profiles.Scores[copy.Id]);
        Assert.Equal("Toán", score.Subject);
        Assert.NotEqual(_profiles.Scores[source.Id][0].Id, score.Id);
        Assert.Single(_profiles.Certs[copy.Id]);
        Assert.Single(_profiles.Activities[copy.Id]);
        Assert.Equal(ProfileTransferStatuses.Copied, Stored(link.Id).ProfileTransferStatus);
    }

    [Fact]
    public async Task AutoShare_ChildHasOwnProfile_OffersAndNeverOverwrites()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);
        AddCommittedProfile(parent);
        var kidProfile = AddCommittedProfile(kid, major: "Biology");

        await CreateService().AutoShareAfterLinkAsync(parent.Id, []);

        Assert.Same(kidProfile, _profiles.ByUser[kid.Id]);
        Assert.Equal("Biology", _profiles.ByUser[kid.Id].IntendedMajor);
        Assert.Equal(ProfileTransferStatuses.Offered, Stored(link.Id).ProfileTransferStatus);

        var offers = await CreateService().GetStudentOffersAsync(kid.Id);
        var offer = Assert.Single(offers.Value!);
        Assert.Equal(link.Id, offer.LinkId);
    }

    [Fact]
    public async Task AutoShare_TwoChildren_WaitsForParentToChoose()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid1 = AddUser("kid1@test.com", UserRoles.Student);
        var kid2 = AddUser("kid2@test.com", UserRoles.Student);
        AddActiveLink(kid1, parent);
        AddActiveLink(kid2, parent);
        AddCommittedProfile(parent);

        await CreateService().AutoShareAfterLinkAsync(parent.Id, []);

        Assert.False(_profiles.ByUser.ContainsKey(kid1.Id));
        Assert.False(_profiles.ByUser.ContainsKey(kid2.Id));
        Assert.All(_links.Links, l => Assert.Null(l.ProfileTransferStatus));
        var overview = await CreateService().GetParentOverviewAsync(parent.Id);
        Assert.True(overview.Value!.HasPendingOwnProfile);
        Assert.Equal(2, overview.Value.Children.Count);
    }

    [Fact]
    public async Task AutoShare_ParentWithoutProfile_DoesNothing()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);

        await CreateService().AutoShareAfterLinkAsync(parent.Id, []);

        Assert.False(_profiles.ByUser.ContainsKey(kid.Id));
        Assert.Null(Stored(link.Id).ProfileTransferStatus);
    }

    [Fact]
    public async Task Share_ParentPicksSecondChild_CopiesOnlyToThatChild_ThenNotAskedAgain()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid1 = AddUser("kid1@test.com", UserRoles.Student);
        var kid2 = AddUser("kid2@test.com", UserRoles.Student);
        AddActiveLink(kid1, parent);
        var link2 = AddActiveLink(kid2, parent);
        AddCommittedProfile(parent);

        var result = await CreateService().ShareAsync(parent.Id, link2.Id);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(ProfileTransferStatuses.Copied, result.Value!.Status);
        Assert.False(_profiles.ByUser.ContainsKey(kid1.Id));
        Assert.Equal("Computer Science", _profiles.ByUser[kid2.Id].IntendedMajor);
        var overview = await CreateService().GetParentOverviewAsync(parent.Id);
        Assert.False(overview.Value!.HasPendingOwnProfile);

        var again = await CreateService().ShareAsync(parent.Id, link2.Id);
        Assert.NotEqual(ParentLinkError.None, again.Error);
    }

    [Fact]
    public async Task Share_Skip_MarksHandledWithoutCopy()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid1 = AddUser("kid1@test.com", UserRoles.Student);
        var kid2 = AddUser("kid2@test.com", UserRoles.Student);
        AddActiveLink(kid1, parent);
        AddActiveLink(kid2, parent);
        AddCommittedProfile(parent);

        var result = await CreateService().ShareAsync(parent.Id, null);

        Assert.Equal(ProfileTransferStatuses.Skipped, result.Value!.Status);
        Assert.Equal(1, _profiles.ByUser.Count);
        Assert.False((await CreateService().GetParentOverviewAsync(parent.Id)).Value!.HasPendingOwnProfile);
    }

    [Theory]
    [InlineData(true, "Computer Science", ProfileTransferStatuses.Copied)]
    [InlineData(false, "Biology", ProfileTransferStatuses.Declined)]
    public async Task DecideOffer_StudentChooses(bool accept, string expectedMajor, string expectedStatus)
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);
        AddCommittedProfile(parent);
        AddCommittedProfile(kid, major: "Biology");
        await CreateService().AutoShareAfterLinkAsync(parent.Id, []);

        var result = await CreateService().DecideOfferAsync(kid.Id, link.Id, accept);

        Assert.Equal(ParentLinkError.None, result.Error);
        Assert.Equal(expectedMajor, _profiles.ByUser[kid.Id].IntendedMajor);
        Assert.Equal(expectedStatus, Stored(link.Id).ProfileTransferStatus);
        Assert.Empty((await CreateService().GetStudentOffersAsync(kid.Id)).Value!);
    }

    [Fact]
    public async Task DecideOffer_OtherStudentsLink_NotFound()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var stranger = AddUser("other@test.com", UserRoles.Student);
        var link = AddActiveLink(kid, parent);
        AddCommittedProfile(parent);
        AddCommittedProfile(kid, major: "Biology");
        await CreateService().AutoShareAfterLinkAsync(parent.Id, []);

        var result = await CreateService().DecideOfferAsync(stranger.Id, link.Id, accept: true);

        Assert.NotEqual(ParentLinkError.None, result.Error);
        Assert.Equal("Biology", _profiles.ByUser[kid.Id].IntendedMajor);
        Assert.False(_profiles.ByUser.ContainsKey(stranger.Id));
    }

    // Hai phụ huynh cùng có hồ sơ: người liên kết trước chép cho con trống; người sau gặp con đã có hồ sơ → con chọn
    [Fact]
    public async Task TwoParents_FirstCopies_SecondBecomesOffer()
    {
        var mom = AddUser("mom@test.com", UserRoles.Parent);
        var dad = AddUser("dad@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        var momLink = AddActiveLink(kid, mom);
        AddCommittedProfile(mom, major: "Economics");
        AddCommittedProfile(dad, major: "Medicine");

        await CreateService().AutoShareAfterLinkAsync(mom.Id, []);
        var dadLink = AddActiveLink(kid, dad);
        await CreateService().AutoShareAfterLinkAsync(dad.Id, []);

        Assert.Equal("Economics", _profiles.ByUser[kid.Id].IntendedMajor);
        Assert.Equal(ProfileTransferStatuses.Copied, Stored(momLink.Id).ProfileTransferStatus);
        Assert.Equal(ProfileTransferStatuses.Offered, Stored(dadLink.Id).ProfileTransferStatus);
    }

    [Fact]
    public async Task Overview_ShowsChildProfileReadOnly_WithoutPendingWhenNoOwnProfile()
    {
        var parent = AddUser("me@test.com", UserRoles.Parent);
        var kid = AddUser("kid@test.com", UserRoles.Student);
        AddActiveLink(kid, parent);

        var overview = await CreateService().GetParentOverviewAsync(parent.Id);

        Assert.False(overview.Value!.HasOwnProfile);
        Assert.False(overview.Value.HasPendingOwnProfile);
        var child = Assert.Single(overview.Value.Children);
        Assert.Equal(kid.Id, child.StudentUserId);
        Assert.Equal(kid.FullName, child.StudentName);
    }
}
