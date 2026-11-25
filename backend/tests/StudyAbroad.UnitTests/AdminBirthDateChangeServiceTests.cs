using StudyAbroad.Application.Audit;
using StudyAbroad.Application.BirthDateChanges;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-150] Administrator xem minh chứng, duyệt, từ chối yêu cầu sửa ngày sinh (E1–E3).</summary>
public class AdminBirthDateChangeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly AuditRequestInfo Info = new("127.0.0.1", "test");
    private static readonly Guid AdminId = Guid.NewGuid();

    private sealed class FakeAudit : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public bool Fail { get; set; }

        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("không ghi được nhật ký");
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    /// <summary>Chạy khối việc như transaction; BeforeWork giả lập thao tác song song xen vào ngay trước khi lưu.</summary>
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Action? BeforeWork { get; set; }

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
        {
            BeforeWork?.Invoke();
            await work(ct);
        }
    }

    private readonly FakeUserRepository _users = new();
    private readonly BirthDateChangeServiceTests.FakeRepo _repo = new();
    private readonly BirthDateChangeServiceTests.FakeStorage _storage = new();
    private readonly FakeAudit _audit = new();
    private readonly RecordingNotifications _notifications = new();

    private readonly FakeUnitOfWork _uow = new();

    private AdminBirthDateChangeService Service() =>
        new(_repo, _users, _storage, _audit, _uow, _notifications, new BirthDateChangeServiceTests.FixedClock(Now));

    private User AddUser(string role, DateOnly? dob)
    {
        var user = new User { Email = $"{Guid.NewGuid():N}@test.local", Role = role, Status = UserStatuses.Active, DateOfBirth = dob, FullName = "Người thử" };
        _users.Users.Add(user);
        return user;
    }

    private BirthDateChangeRequest AddRequest(User user, DateOnly newDob, bool withEvidence = true, string status = BirthDateChangeStatuses.Pending)
    {
        var request = new BirthDateChangeRequest
        {
            UserId = user.Id,
            CurrentBirthDate = user.DateOfBirth,
            RequestedBirthDate = newDob,
            Reason = "Nhập nhầm năm sinh",
            Status = status,
        };
        if (withEvidence)
        {
            var path = $"uploads/birth-date-evidence/{request.Id:N}/file.pdf";
            _storage.Files[path] = [1, 2, 3];
            request.EvidencePath = path;
            request.EvidenceType = BirthDateEvidenceTypes.CitizenId;
            request.EvidenceFileName = "cccd.pdf";
            request.EvidenceContentType = "application/pdf";
        }
        _repo.Items.Add(request);
        return request;
    }

    [Fact]
    public async Task Approve_ChangesBirthDate_ClosesRequest_DeletesEvidence_LogsAndNotifies()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1)); // 16
        var request = AddRequest(student, new DateOnly(2008, 5, 1)); // 18
        var evidencePath = request.EvidencePath!;

        var result = await Service().ApproveAsync(request.Id, AdminId, Info);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new DateOnly(2008, 5, 1), _users.Users.Single().DateOfBirth);
        var stored = _repo.Items.Single();
        Assert.Equal(BirthDateChangeStatuses.Approved, stored.Status);
        Assert.Equal(AdminId, stored.ReviewedByUserId);
        Assert.Null(stored.EvidencePath);
        Assert.Equal(BirthDateEvidenceTypes.CitizenId, stored.EvidenceType);
        Assert.Contains(evidencePath, _storage.Deleted);

        var log = Assert.Single(_audit.Entries);
        Assert.Equal(AdminBirthDateChangeService.ActionApproved, log.Action);
        Assert.Equal(AdminId, log.ActorUserId);
        Assert.Contains("\"oldBirthDate\":\"2010-05-01\"", log.DetailJson);
        Assert.Contains("\"newBirthDate\":\"2008-05-01\"", log.DetailJson);
        Assert.Contains(BirthDateEvidenceTypes.CitizenId, log.DetailJson);
        // Nhật ký không lưu đường dẫn tệp minh chứng
        Assert.DoesNotContain("uploads/", log.DetailJson);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal(student.Id, sent.UserId);
        Assert.Equal(AdminBirthDateChangeService.NotificationApproved, sent.Type);
    }

    [Theory]
    [InlineData(BirthDateChangeStatuses.Cancelled, "Người dùng đã hủy")]
    [InlineData(BirthDateChangeStatuses.Approved, "đã được xử lý")]
    [InlineData(BirthDateChangeStatuses.Rejected, "đã được xử lý")]
    public async Task Approve_RequestNotPending_ReturnsConflict_E1(string status, string message)
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2008, 5, 1), withEvidence: false, status: status);

        var result = await Service().ApproveAsync(request.Id, AdminId, Info);

        Assert.Equal(BirthDateChangeError.Conflict, result.Error);
        Assert.Contains(message, result.Message);
        Assert.Equal(new DateOnly(2010, 5, 1), _users.Users.Single().DateOfBirth);
        Assert.Empty(_audit.Entries);
        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task Approve_UserCancelledMeanwhile_DoesNotChangeBirthDate_E1()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2008, 5, 1));
        // Người dùng hủy sau khi service đọc yêu cầu nhưng trước khi lưu: TryClose thua
        _uow.BeforeWork = () => _repo.Items.Single().Status = BirthDateChangeStatuses.Cancelled;

        var result = await Service().ApproveAsync(request.Id, AdminId, Info);

        Assert.Equal(BirthDateChangeError.Conflict, result.Error);
        Assert.Contains("hủy", result.Message);
        Assert.Equal(new DateOnly(2010, 5, 1), _users.Users.Single().DateOfBirth);
        Assert.Empty(_audit.Entries);
        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task Approve_UnknownRequest_NotFound()
    {
        var result = await Service().ApproveAsync(Guid.NewGuid(), AdminId, Info);
        Assert.Equal(BirthDateChangeError.NotFound, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Reject_WithoutReason_E2(string? reason)
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2008, 5, 1));

        var result = await Service().RejectAsync(request.Id, AdminId, reason, Info);

        Assert.Equal(BirthDateChangeError.Validation, result.Error);
        Assert.Equal(BirthDateChangeStatuses.Pending, _repo.Items.Single().Status);
        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task Reject_ReasonTooLong_E2()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2008, 5, 1));

        var result = await Service().RejectAsync(request.Id, AdminId, new string('a', 501), Info);

        Assert.Equal(BirthDateChangeError.Validation, result.Error);
    }

    [Fact]
    public async Task Reject_KeepsBirthDate_StoresReason_DeletesEvidence_LogsAndNotifies()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2008, 5, 1));
        var evidencePath = request.EvidencePath!;

        var result = await Service().RejectAsync(request.Id, AdminId, "  Ảnh căn cước bị mờ  ", Info);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new DateOnly(2010, 5, 1), _users.Users.Single().DateOfBirth);
        var stored = _repo.Items.Single();
        Assert.Equal(BirthDateChangeStatuses.Rejected, stored.Status);
        Assert.Equal("Ảnh căn cước bị mờ", stored.RejectReason);
        Assert.Null(stored.EvidencePath);
        Assert.Contains(evidencePath, _storage.Deleted);
        Assert.Equal("Ảnh căn cước bị mờ", result.Value!.RejectReason);

        var log = Assert.Single(_audit.Entries);
        Assert.Equal(AdminBirthDateChangeService.ActionRejected, log.Action);
        Assert.Contains("rejectReason", log.DetailJson);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal(AdminBirthDateChangeService.NotificationRejected, sent.Type);
        Assert.Contains("Ảnh căn cước bị mờ", sent.Body);
    }

    [Fact]
    public async Task Reject_AlreadyCancelled_E1()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2008, 5, 1), withEvidence: false, status: BirthDateChangeStatuses.Cancelled);

        var result = await Service().RejectAsync(request.Id, AdminId, "Không hợp lệ", Info);

        Assert.Equal(BirthDateChangeError.Conflict, result.Error);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task OpenEvidence_LogsAccessBeforeReturningFile()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2008, 5, 1));

        var result = await Service().OpenEvidenceAsync(request.Id, AdminId, Info);

        Assert.True(result.Success, result.Message);
        Assert.Equal("application/pdf", result.Value!.ContentType);
        Assert.Equal("cccd.pdf", result.Value.FileName);
        var log = Assert.Single(_audit.Entries);
        Assert.Equal(AuditLogConstants.BirthDateEvidenceViewed, log.Action);
        Assert.Contains(AuditLogConstants.BirthDateEvidenceViewed, AuditLogConstants.SensitiveActions);
        Assert.Equal(AdminId, log.ActorUserId);
        Assert.Equal("127.0.0.1", log.IpAddress);
        Assert.Equal(request.Id.ToString(), log.EntityId);
    }

    [Fact]
    public async Task OpenEvidence_AuditFails_DoesNotReturnFile_E3()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2008, 5, 1));
        _audit.Fail = true;

        var result = await Service().OpenEvidenceAsync(request.Id, AdminId, Info);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains("nhật ký", result.Message);
    }

    [Fact]
    public async Task OpenEvidence_NoEvidence_NotFound_AndNoLog()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2010, 5, 1));
        var request = AddRequest(student, new DateOnly(2010, 6, 1), withEvidence: false);

        var result = await Service().OpenEvidenceAsync(request.Id, AdminId, Info);

        Assert.Equal(BirthDateChangeError.NotFound, result.Error);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Get_ShowsAgesAndCrossedThresholds()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2011, 5, 1)); // 15
        var request = AddRequest(student, new DateOnly(2008, 5, 1)); // 18

        var result = await Service().GetAsync(request.Id);

        Assert.True(result.Success);
        var dto = result.Value!;
        Assert.Equal(15, dto.CurrentAge);
        Assert.Equal(18, dto.RequestedAge);
        Assert.Equal([16, 18], dto.CrossedThresholds);
        Assert.True(dto.HasEvidence);
        Assert.Equal(student.Email, dto.Email);
    }

    [Theory]
    [InlineData(17, 15, new[] { 16 })]   // xuống dưới 16
    [InlineData(19, 17, new[] { 18 })]   // xuống dưới 18
    [InlineData(17, 17, new int[0])]
    [InlineData(null, 16, new[] { 16 })]
    [InlineData(null, 15, new int[0])]
    public void CrossedThresholds_BothDirections(int? oldAge, int newAge, int[] expected)
    {
        Assert.Equal(expected, AdminBirthDateChangeService.CrossedThresholds(oldAge, newAge));
    }
}
