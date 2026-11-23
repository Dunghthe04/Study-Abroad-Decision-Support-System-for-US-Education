using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [UC-61] Khóa tài khoản trung tâm đưa trung tâm về Tạm ngừng, đóng yêu cầu liên hệ đang mở và báo người dùng liên quan;
/// [UC-62] mở khóa khôi phục trạng thái trước khi Tạm ngừng.
/// </summary>
public class CenterAccountLockTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool VerifyPassword(string hash, string password) => hash == "hashed:" + password;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public (string Token, DateTime ExpiresAt) GenerateToken(User user) => ("token", DateTime.UtcNow.AddDays(1));
        public string HashToken(string token) => "hash:" + token;
    }

    private sealed class FakeCenters : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];
        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<StudyCenter>(Centers, 1, Centers.Count, Centers.Count));
        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StudyCenter>>(Centers);
        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Centers.Any(c => c.Code == code));
        public Task AddAsync(StudyCenter center, CancellationToken ct = default) { Centers.Add(center); return Task.CompletedTask; }
        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.OwnerUserId == ownerUserId));
    }

    private sealed class FakeWork : ICenterDeletionWorkRepository
    {
        public List<ClosedLeadRecipients> OpenLeads { get; } = [];
        public List<Notification> Notifications { get; } = [];
        public int CloseCalls { get; private set; }

        public Task<IReadOnlyList<ClosedLeadRecipients>> CloseOpenLeadsAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default)
        {
            CloseCalls++;
            IReadOnlyList<ClosedLeadRecipients> closed = OpenLeads.ToList();
            OpenLeads.Clear();
            return Task.FromResult(closed);
        }

        public Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default)
        {
            Notifications.AddRange(notifications);
            return Task.CompletedTask;
        }

        public Task<PagedResult<AdminCenterDeletionRequestDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AdminCenterDeletionRequestDto?> GetViewAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<Guid>> GetDuePurgeRequestIdsAsync(DateTime now, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountOpenReportsAsync(Guid studyCenterId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> LockCenterIfPurgeableAsync(Guid studyCenterId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AnonymizeOwnerAccountAsync(Guid userId, DateTime now, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ErasedLeadMessages> ErasePrivateLeadMessagesAsync(Guid studyCenterId, Guid? ownerUserId, DateTime now, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> EraseResolvedCenterMessagesAsync(DateTime now, CancellationToken ct = default) => throw new NotImplementedException();
        public Task PurgeCenterDataAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteCenterFilesAsync(Guid studyCenterId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetCenterMediaUrlsAsync(Guid studyCenterId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private readonly FakeUserRepository _users = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _audit = new();
    private readonly FakeCenters _centers = new();
    private readonly FakeWork _work = new();

    private readonly User _admin = new() { Id = Guid.NewGuid(), Email = "admin@example.com", FullName = "Admin", Role = UserRoles.Admin, Status = UserStatuses.Active };
    private readonly User _owner = new() { Id = Guid.NewGuid(), Email = "trungtam@example.com", FullName = "Trung tâm ABC", Role = UserRoles.Center, Status = UserStatuses.Active };

    private AuthService Service() => new(
        _users, new FakeHasher(), new FakeTokenService(), _email, _links, _audit,
        new ParentLinkService(_links, _users, _email, _audit), new FakeUnitOfWork(), new FakeLeadWithdrawalRepository(),
        studyCenters: _centers, centerWork: _work);

    private StudyCenter Arrange(string status)
    {
        _users.Users.Add(_admin);
        _users.Users.Add(_owner);
        var center = new StudyCenter { Id = Guid.NewGuid(), Name = "Trung tâm ABC", Code = "CTR_1", OwnerUserId = _owner.Id, VerificationStatus = status };
        _centers.Centers.Add(center);
        return center;
    }

    [Fact]
    public async Task Lock_CenterAccount_SuspendsCenterClosesOpenLeadsAndNotifiesStudentAndParent()
    {
        var center = Arrange(CenterVerificationStatuses.Verified);
        var student = Guid.NewGuid();
        var parent = Guid.NewGuid();
        _work.OpenLeads.Add(new ClosedLeadRecipients(Guid.NewGuid(), student, parent));
        _work.OpenLeads.Add(new ClosedLeadRecipients(Guid.NewGuid(), student, null));

        var result = await Service().LockUserAccountAsync(_owner.Id, new LockUserAccountRequest("Vi phạm quy định"), _admin.Id);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Locked, _owner.Status);
        Assert.Equal(CenterVerificationStatuses.Suspended, center.VerificationStatus);
        Assert.Equal(1, _work.CloseCalls);
        Assert.Equal(2, _work.Notifications.Count);
        Assert.Contains(_work.Notifications, n => n.UserId == student);
        Assert.Contains(_work.Notifications, n => n.UserId == parent);
        Assert.All(_work.Notifications, n => Assert.Equal(AuthService.CenterSuspendedLeadClosedNotificationType, n.Type));
        var audit = Assert.Single(_audit.Entries, e => e.Action == "user.lock");
        Assert.Contains("\"centerPreviousStatus\":\"verified\"", audit.DetailJson);
    }

    [Fact]
    public async Task Lock_StudentAccount_DoesNotTouchAnyCenter()
    {
        var center = Arrange(CenterVerificationStatuses.Verified);
        var student = new User { Id = Guid.NewGuid(), Email = "hs@example.com", FullName = "HS", Role = UserRoles.Student, Status = UserStatuses.Active };
        _users.Users.Add(student);

        var result = await Service().LockUserAccountAsync(student.Id, new LockUserAccountRequest("Spam"), _admin.Id);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(CenterVerificationStatuses.Verified, center.VerificationStatus);
        Assert.Equal(0, _work.CloseCalls);
    }

    [Theory]
    [InlineData(CenterVerificationStatuses.Verified)]
    [InlineData(CenterVerificationStatuses.PendingReview)]
    [InlineData(CenterVerificationStatuses.Rejected)]
    public async Task Unlock_CenterAccount_RestoresTheStatusBeforeSuspension(string before)
    {
        var center = Arrange(before);
        await Service().LockUserAccountAsync(_owner.Id, new LockUserAccountRequest("Vi phạm"), _admin.Id);
        Assert.Equal(CenterVerificationStatuses.Suspended, center.VerificationStatus);

        var result = await Service().UnlockUserAccountAsync(_owner.Id, new UnlockUserAccountRequest("Đã làm rõ"), _admin.Id);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Active, _owner.Status);
        Assert.Equal(before, center.VerificationStatus);
    }

    [Fact]
    public async Task Unlock_CenterRevokedByModeratorBeforeLock_StaysSuspended()
    {
        // Dấu xác minh đã bị thu hồi (UC-59) trước khi khóa tài khoản: mở khóa không tự bật lại trạng thái
        var center = Arrange(CenterVerificationStatuses.Suspended);
        await Service().LockUserAccountAsync(_owner.Id, new LockUserAccountRequest("Vi phạm"), _admin.Id);

        await Service().UnlockUserAccountAsync(_owner.Id, new UnlockUserAccountRequest("Mở lại"), _admin.Id);

        Assert.Equal(UserStatuses.Active, _owner.Status);
        Assert.Equal(CenterVerificationStatuses.Suspended, center.VerificationStatus);
    }

    [Fact]
    public async Task Unlock_ClosedLeadsAreNotReopened()
    {
        Arrange(CenterVerificationStatuses.Verified);
        _work.OpenLeads.Add(new ClosedLeadRecipients(Guid.NewGuid(), Guid.NewGuid(), null));
        await Service().LockUserAccountAsync(_owner.Id, new LockUserAccountRequest("Vi phạm"), _admin.Id);

        await Service().UnlockUserAccountAsync(_owner.Id, new UnlockUserAccountRequest("Mở lại"), _admin.Id);

        Assert.Empty(_work.OpenLeads);
        Assert.Equal(1, _work.CloseCalls);
    }
}
