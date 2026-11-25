using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-80] Job xóa hẳn tài khoản: thứ tự các bước, bỏ qua tài khoản vừa khôi phục, lỗi một người không chặn người khác.</summary>
public class AccountPurgeServiceTests
{
    private sealed class FakePurgeRepository : IAccountPurgeRepository
    {
        public List<Guid> DueIds { get; } = [];
        /// <summary>Id không còn thỏa điều kiện lúc kiểm tra lại (vừa khôi phục).</summary>
        public HashSet<Guid> Restored { get; } = [];
        /// <summary>Id làm bước xóa user ném lỗi.</summary>
        public HashSet<Guid> FailOnDelete { get; } = [];
        public List<string> Steps { get; } = [];
        public DateTime? LastDeletedBefore { get; private set; }
        public List<string>? LastRoles { get; private set; }

        public Task<List<Guid>> GetDueUserIdsAsync(DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default)
        {
            LastDeletedBefore = deletedBefore;
            LastRoles = roles.ToList();
            return Task.FromResult(DueIds.ToList());
        }

        public Task<bool> LockIfStillDueAsync(Guid userId, DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default)
        {
            Steps.Add($"lock:{userId}");
            return Task.FromResult(!Restored.Contains(userId));
        }

        public Task<int> ClearPaymentPayloadsAsync(Guid userId, CancellationToken ct = default)
        {
            Steps.Add($"payments:{userId}");
            return Task.FromResult(1);
        }

        public Task<ForumPurgeResult> PurgeForumPostsAsync(Guid userId, DateTime now, CancellationToken ct = default)
        {
            Steps.Add($"forum:{userId}");
            return Task.FromResult(new ForumPurgeResult(1, 1));
        }

        public Task<int> RetainBuyerIdentityAsync(Guid userId, DateTime now, CancellationToken ct = default)
        {
            Steps.Add($"buyer:{userId}");
            return Task.FromResult(1);
        }

        public Task ScrubRetainedLeadsAsync(Guid userId, CancellationToken ct = default)
        {
            Steps.Add($"leads:{userId}");
            return Task.CompletedTask;
        }

        public Task<int> AnonymizeAiUsageAsync(Guid userId, CancellationToken ct = default)
        {
            Steps.Add($"ai:{userId}");
            return Task.FromResult(1);
        }

        public DateTime? LastRetainedPurgeAt { get; private set; }

        public Task<RetainedLeadPurgeResult> PurgeExpiredLeadMessagesAsync(DateTime now, CancellationToken ct = default)
        {
            LastRetainedPurgeAt = now;
            Steps.Add("expired-messages");
            return Task.FromResult(new RetainedLeadPurgeResult(3, 1));
        }

        public Task<(int Orders, int Buyers)> PurgeExpiredOrdersAsync(DateTime now, CancellationToken ct = default)
        {
            Steps.Add("expired-orders");
            return Task.FromResult((2, 1));
        }

        public Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
        {
            if (FailOnDelete.Contains(userId)) throw new InvalidOperationException("db lỗi");
            Steps.Add($"delete:{userId}");
            return Task.CompletedTask;
        }
    }

    /// <summary>Ghi lại thời điểm xóa tệp so với các bước DB (dùng chung danh sách Steps).</summary>
    private sealed class FakeFileStorage(List<string> steps) : IProfileFileStorageService
    {
        public HashSet<Guid> FailFor { get; } = [];

        public Task DeleteAllUserFilesAsync(Guid userId, CancellationToken ct = default)
        {
            if (FailFor.Contains(userId)) throw new IOException("tệp đang bị khóa");
            steps.Add($"files:{userId}");
            return Task.CompletedTask;
        }

        public DraftTranscriptInfoDto ValidateTranscriptFile(byte[] fileBytes, string fileName) => throw new NotSupportedException();
        public Task DeleteDraftFileAsync(Guid userId, string? draftRelativePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteTranscriptFileAsync(Guid userId, string? transcriptRelativePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteDraftFilesAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>Hook gói dịch vụ giả: ghi bước vào cùng danh sách Steps để kiểm thứ tự so với DeleteUserAsync.</summary>
    private sealed class FakeBillingHooks(List<string> steps) : IBillingAccountHooks
    {
        public HashSet<Guid> FailFor { get; } = [];

        public Task<Func<CancellationToken, Task>> OnAccountSoftDeletedAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Func<CancellationToken, Task>> OnBeforePurgeAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
        {
            if (FailFor.Contains(userId)) throw new InvalidOperationException("hook lỗi");
            steps.Add($"billing:{userId}");
            return Task.FromResult<Func<CancellationToken, Task>>(_ =>
            {
                steps.Add($"billing-after:{userId}");
                return Task.CompletedTask;
            });
        }
    }

    private readonly FakePurgeRepository _repo = new();
    private readonly FakeFileStorage _files;
    private readonly FakeAuditLogRepository _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly AccountDeletionOptions _options = new() { RetentionPeriod = TimeSpan.FromDays(30) };
    private static readonly DateTime Now = new(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeBillingHooks _billing;

    public AccountPurgeServiceTests()
    {
        _files = new FakeFileStorage(_repo.Steps);
        _billing = new FakeBillingHooks(_repo.Steps);
    }

    private AccountPurgeService CreateService(bool withBillingHooks = false) =>
        new(_repo, _audit, _unitOfWork, _files, _options, withBillingHooks ? _billing : null);

    [Fact]
    public async Task Purge_UsesRetentionCutoffAndOnlyStudentParentRoles()
    {
        await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(Now.AddDays(-30), _repo.LastDeletedBefore);
        Assert.NotNull(_repo.LastRoles);
        Assert.Equal(new[] { UserRoles.Parent, UserRoles.Student }, _repo.LastRoles!.OrderBy(r => r));
    }

    [Fact]
    public async Task Purge_DueUser_RunsStepsInOrderInOneTransactionAndAuditsIdOnly()
    {
        var id = Guid.NewGuid();
        _repo.DueIds.Add(id);

        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(1, summary.Purged);
        Assert.Equal(0, summary.Skipped);
        Assert.Empty(summary.Failures);
        Assert.Equal(1, _unitOfWork.Calls);
        // Tệp xóa sau cùng, ngoài transaction
        Assert.Equal(new[] { $"lock:{id}", $"payments:{id}", $"buyer:{id}", $"leads:{id}", $"ai:{id}", $"forum:{id}", $"delete:{id}", $"files:{id}" }, _repo.Steps);
        Assert.Empty(summary.FileCleanupFailures);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AccountPurgeService.AuditAction, entry.Action);
        Assert.Null(entry.ActorUserId);
        Assert.Equal("user", entry.EntityType);
        Assert.Equal(id.ToString(), entry.EntityId);
        Assert.Null(entry.DetailJson);
    }

    [Fact]
    public async Task Purge_BillingHook_RunsInsideTheTransactionBeforeDeleteUser_AndAfterCommitWorkRunsBeforeFiles()
    {
        var id = Guid.NewGuid();
        _repo.DueIds.Add(id);

        var summary = await CreateService(withBillingHooks: true).PurgeDueAccountsAsync(Now);

        Assert.Equal(1, summary.Purged);
        Assert.Equal(1, _unitOfWork.Calls);
        // Hook ngay trước khi xóa user (cùng transaction); việc sau commit (báo thành viên) chạy sau khi xóa, trước khi dọn tệp
        Assert.Equal(
            new[] { $"lock:{id}", $"payments:{id}", $"buyer:{id}", $"leads:{id}", $"ai:{id}", $"forum:{id}", $"billing:{id}", $"delete:{id}", $"billing-after:{id}", $"files:{id}" },
            _repo.Steps);
    }

    [Fact]
    public async Task Purge_BillingHookFails_UserNotDeletedAndReported_OthersStillPurged()
    {
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        _repo.DueIds.AddRange([bad, good]);
        _billing.FailFor.Add(bad);

        var summary = await CreateService(withBillingHooks: true).PurgeDueAccountsAsync(Now);

        Assert.Equal(1, summary.Purged);
        Assert.Equal(bad, Assert.Single(summary.Failures).UserId);
        Assert.DoesNotContain($"delete:{bad}", _repo.Steps);
        Assert.DoesNotContain($"billing-after:{bad}", _repo.Steps);
        Assert.DoesNotContain($"files:{bad}", _repo.Steps);
        Assert.Contains($"delete:{good}", _repo.Steps);
        Assert.Equal(good.ToString(), Assert.Single(_audit.Entries).EntityId);
    }

    [Fact]
    public async Task Purge_UserRestoredMeanwhile_BillingHookNotCalled()
    {
        var id = Guid.NewGuid();
        _repo.DueIds.Add(id);
        _repo.Restored.Add(id);

        await CreateService(withBillingHooks: true).PurgeDueAccountsAsync(Now);

        Assert.Equal(new[] { $"lock:{id}" }, _repo.Steps);
    }

    [Fact]
    public async Task Purge_UserRestoredMeanwhile_SkipsWithoutTouchingData()
    {
        var id = Guid.NewGuid();
        _repo.DueIds.Add(id);
        _repo.Restored.Add(id);

        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(0, summary.Purged);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(new[] { $"lock:{id}" }, _repo.Steps);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Purge_OneUserFails_OthersStillPurgedAndFailureReported()
    {
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        _repo.DueIds.AddRange([bad, good]);
        _repo.FailOnDelete.Add(bad);

        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(1, summary.Purged);
        var failure = Assert.Single(summary.Failures);
        Assert.Equal(bad, failure.UserId);
        Assert.IsType<InvalidOperationException>(failure.Error);
        Assert.Contains($"delete:{good}", _repo.Steps);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(good.ToString(), entry.EntityId);
    }

    [Fact]
    public async Task Purge_FailedUser_FilesNotDeleted()
    {
        var id = Guid.NewGuid();
        _repo.DueIds.Add(id);
        _repo.FailOnDelete.Add(id);

        await CreateService().PurgeDueAccountsAsync(Now);

        // DB hoàn tác nên tài khoản còn nguyên: tệp cũng phải còn
        Assert.DoesNotContain($"files:{id}", _repo.Steps);
    }

    [Fact]
    public async Task Purge_FileDeleteFails_StillCountedPurgedAndReportedSeparately()
    {
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        _repo.DueIds.AddRange([bad, good]);
        _files.FailFor.Add(bad);

        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(2, summary.Purged);
        Assert.Empty(summary.Failures);
        var fileFailure = Assert.Single(summary.FileCleanupFailures);
        Assert.Equal(bad, fileFailure.UserId);
        Assert.IsType<IOException>(fileFailure.Error);
        Assert.Contains($"files:{good}", _repo.Steps);
        Assert.Equal(2, _audit.Entries.Count);
    }

    [Fact]
    public async Task Purge_NoDueUsers_DoesNothing()
    {
        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(0, summary.Purged);
        Assert.Equal(0, _unitOfWork.Calls);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Purge_Cancelled_Throws()
    {
        _repo.DueIds.Add(Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().PurgeDueAccountsAsync(Now, cts.Token));
    }

    [Fact]
    public async Task PurgeExpiredRetainedData_RunsMessagesThenOrders_InOneTransaction()
    {
        var summary = await CreateService().PurgeExpiredRetainedDataAsync(Now);

        Assert.Equal(new[] { "expired-messages", "expired-orders" }, _repo.Steps);
        Assert.Equal(Now, _repo.LastRetainedPurgeAt);
        Assert.Equal(1, _unitOfWork.Calls);
        Assert.Equal(new RetainedDataPurgeSummary(3, 1, 2, 1), summary);
    }

    [Theory]
    // Giao dịch năm 2026 (giờ Việt Nam) giữ tới hết 31/12/2036, tức 0h 1/1/2037 giờ Việt Nam
    [InlineData("2026-03-15T10:00:00Z", "2036-12-31T17:00:00Z")]
    // 31/12/2026 20h UTC đã là 1/1/2027 giờ Việt Nam → thuộc năm tài chính 2027
    [InlineData("2026-12-31T20:00:00Z", "2037-12-31T17:00:00Z")]
    public void OrderRetainUntil_TenYearsAfterFiscalYearEnd(string transaction, string expected)
    {
        var until = RetentionPeriods.OrderRetainUntil(DateTime.Parse(transaction, null, System.Globalization.DateTimeStyles.AdjustToUniversal));

        Assert.Equal(DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal), until);
    }

    [Fact]
    public void OrderPurgeCutoff_MatchesRetainUntil()
    {
        var tx = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var until = RetentionPeriods.OrderRetainUntil(tx);

        // Ngay trước mốc: đơn chưa hết hạn; đúng mốc: đã hết hạn
        Assert.False(tx < RetentionPeriods.OrderPurgeCutoff(until.AddSeconds(-1)));
        Assert.True(tx < RetentionPeriods.OrderPurgeCutoff(until));
    }
}
