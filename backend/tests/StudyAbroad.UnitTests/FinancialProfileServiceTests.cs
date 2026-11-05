using StudyAbroad.Application.Profile.Financial;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [USAS-364] Unit Tests cho FinancialProfileService.
/// Kiểm thử lưu trữ trên thực thể StudentProfile và ProfileActivity theo kiến trúc chuẩn của nhóm.
/// </summary>
public class FinancialProfileServiceTests
{
    private sealed class FakeFinancialProfileRepository : IFinancialProfileRepository
    {
        private readonly List<StudentProfile> _profiles = [];
        private readonly List<ProfileActivity> _activities = [];

        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_profiles.FirstOrDefault(p => p.UserId == userId));

        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default)
        {
            var p = _profiles.FirstOrDefault(x => x.UserId == userId);
            if (p is not null) return Task.FromResult(p);

            p = new StudentProfile { UserId = userId, TargetLevel = "undergraduate" };
            _profiles.Add(p);
            return Task.FromResult(p);
        }

        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default)
        {
            var idx = _profiles.FindIndex(p => p.Id == profile.Id);
            if (idx >= 0) _profiles[idx] = profile;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProfileActivity>> GetActivitiesByProfileIdAsync(Guid studentProfileId, string? kind = null, CancellationToken ct = default)
        {
            var q = _activities.Where(a => a.StudentProfileId == studentProfileId);
            if (!string.IsNullOrEmpty(kind)) q = q.Where(a => a.Kind == kind);
            return Task.FromResult<IReadOnlyList<ProfileActivity>>(q.ToList());
        }

        public Task<ProfileActivity?> GetActivityByIdAsync(Guid activityId, CancellationToken ct = default) =>
            Task.FromResult(_activities.FirstOrDefault(a => a.Id == activityId));

        public Task AddActivityAsync(ProfileActivity activity, CancellationToken ct = default)
        {
            _activities.Add(activity);
            return Task.CompletedTask;
        }

        public Task UpdateActivityAsync(ProfileActivity activity, CancellationToken ct = default)
        {
            var idx = _activities.FindIndex(a => a.Id == activity.Id);
            if (idx >= 0) _activities[idx] = activity;
            return Task.CompletedTask;
        }

        public Task DeleteActivityAsync(ProfileActivity activity, CancellationToken ct = default)
        {
            _activities.Remove(activity);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SaveFinancialProfileAsync_SavesBudgetAndFundingSource()
    {
        var repo = new FakeFinancialProfileRepository();
        var service = new FinancialProfileService(repo);
        var userId = Guid.NewGuid();

        var request = new SaveFinancialProfileRequest(
            AnnualBudget: 35000,
            FundingSource: "family_support",
            NeedScholarship: true
        );

        var result = await service.SaveFinancialProfileAsync(userId, request);

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(35000, result.AnnualBudget);
        Assert.Equal("family_support", result.FundingSource);
        Assert.True(result.NeedScholarship);
    }

    [Fact]
    public async Task SaveFinancialProfileAsync_UpdatesExistingProfile_WithoutDuplication()
    {
        var repo = new FakeFinancialProfileRepository();
        var service = new FinancialProfileService(repo);
        var userId = Guid.NewGuid();

        await service.SaveFinancialProfileAsync(userId, new SaveFinancialProfileRequest(
            AnnualBudget: 25000,
            FundingSource: "personal_savings",
            NeedScholarship: false
        ));

        var updated = await service.SaveFinancialProfileAsync(userId, new SaveFinancialProfileRequest(
            AnnualBudget: 40000,
            FundingSource: "bank_loan",
            NeedScholarship: true
        ));

        Assert.Equal(40000, updated.AnnualBudget);
        Assert.Equal("bank_loan", updated.FundingSource);
        Assert.True(updated.NeedScholarship);
    }

    [Fact]
    public async Task SaveFinancialProfileAsync_ThrowsArgumentException_WhenBudgetNegative()
    {
        var service = new FinancialProfileService(new FakeFinancialProfileRepository());
        var userId = Guid.NewGuid();

        var request = new SaveFinancialProfileRequest(
            AnnualBudget: -1000,
            FundingSource: "family",
            NeedScholarship: false
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveFinancialProfileAsync(userId, request));
    }

    [Fact]
    public async Task AddActivityAsync_CreatesProfileActivityWithExtracurricularKind()
    {
        var repo = new FakeFinancialProfileRepository();
        var service = new FinancialProfileService(repo);
        var userId = Guid.NewGuid();

        var created = await service.AddActivityAsync(userId, new CreateExtracurricularRequest(
            ActivityName: "CLB Tình nguyện",
            Role: "Trưởng ban Hậu cần",
            ImpactLevel: 3
        ));

        Assert.NotNull(created);
        Assert.Equal("CLB Tình nguyện", created.ActivityName);
        Assert.Equal("Trưởng ban Hậu cần", created.Role);

        var list = await service.GetActivitiesAsync(userId);
        Assert.Single(list);
    }

    [Fact]
    public async Task AddAndUpdateActivity_KeepsDurationImpactAndOngoingAfterReload()
    {
        var repo = new FakeFinancialProfileRepository();
        var service = new FinancialProfileService(repo);
        var userId = Guid.NewGuid();

        var created = await service.AddActivityAsync(userId, new CreateExtracurricularRequest(
            ActivityName: "CLB Robotics", Role: "Chủ nhiệm", DurationMonths: 12, IsOngoing: true, ImpactLevel: 4));

        var loaded = Assert.Single(await service.GetActivitiesAsync(userId));   // tải lại trang: đọc từ DB, không từ request
        Assert.Equal(12, loaded.DurationMonths);
        Assert.True(loaded.IsOngoing);
        Assert.Equal(4, loaded.ImpactLevel);

        await service.UpdateActivityAsync(userId, created.Id, new UpdateExtracurricularRequest(
            ActivityName: "CLB Robotics", Role: "Chủ nhiệm", DurationMonths: 18, IsOngoing: false, ImpactLevel: 2));

        loaded = Assert.Single(await service.GetActivitiesAsync(userId));
        Assert.Equal(18, loaded.DurationMonths);
        Assert.False(loaded.IsOngoing);
        Assert.Equal(2, loaded.ImpactLevel);
    }

    [Fact]
    public async Task UpdateActivityAsync_RejectsWhenUserDoesNotOwnProfile_AntiIDOR()
    {
        var repo = new FakeFinancialProfileRepository();
        var service = new FinancialProfileService(repo);
        var userAlice = Guid.NewGuid();
        var userBob = Guid.NewGuid();

        var aliceAct = await service.AddActivityAsync(userAlice, new CreateExtracurricularRequest(
            ActivityName: "Dự án của Alice",
            Role: "Leader"
        ));

        // Bob cố tình cập nhật hoạt động của Alice
        var bobResult = await service.UpdateActivityAsync(userBob, aliceAct.Id, new UpdateExtracurricularRequest(
            ActivityName: "Bob sửa",
            Role: "Hacker",
            ImpactLevel: 1
        ));

        Assert.Null(bobResult);
    }

    [Fact]
    public async Task DeleteActivityAsync_RejectsWhenUserDoesNotOwnProfile_AntiIDOR()
    {
        var repo = new FakeFinancialProfileRepository();
        var service = new FinancialProfileService(repo);
        var userAlice = Guid.NewGuid();
        var userBob = Guid.NewGuid();

        var aliceAct = await service.AddActivityAsync(userAlice, new CreateExtracurricularRequest(
            ActivityName: "Hoạt động Alice",
            Role: "Member"
        ));

        var bobDeleteResult = await service.DeleteActivityAsync(userBob, aliceAct.Id);
        Assert.False(bobDeleteResult);

        var aliceList = await service.GetActivitiesAsync(userAlice);
        Assert.Single(aliceList);
    }
}
